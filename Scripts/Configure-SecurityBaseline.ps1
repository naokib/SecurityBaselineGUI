<#
.SYNOPSIS
    Windows LAPS / LSA Protection / ASR(攻撃対象領域の減少)を GPO 経由で一括展開する。

.DESCRIPTION
    以下を対象 OU に対して設定する。
      1. Windows LAPS  … スキーマ拡張(必要な場合)、対象OUへの委任、パスワードポリシー
      2. LSA Protection … lsass.exe の PPL(保護プロセス)化 (RunAsPPL)
      3. ASR ルール     … Microsoft 推奨16ルールを既定で監査モードで有効化

    GPO作成・リンク・スキーマ拡張は ShouldProcess 対応。-WhatIf で事前確認できる。

.PARAMETER TargetOU
    LAPS委任・GPOリンク対象のOUの識別名 (DistinguishedName)。例: "OU=Workstations,DC=contoso,DC=com"

.PARAMETER GPOName
    作成/更新するGPO名。

.PARAMETER AsrAction
    ASRルールの適用モード。既定は Audit(監査のみ、業務影響なし)。
    実環境でログを確認し問題なければ Block に切り替えて再実行する。

.PARAMETER LsaProtectionUefiLock
    指定するとLSA ProtectionをUEFIロック付き(RunAsPPL=2)にする。
    ロック付きはセキュアブート経由でしか解除できず、誤設定時の復旧が難しいため既定はオフ。

.PARAMETER LapsPasswordLength
    LAPSパスワードの長さ。既定 20。

.PARAMETER LapsPasswordAgeDays
    LAPSパスワードのローテーション間隔(日数)。既定 30。

.PARAMETER SkipSchemaExtension
    AD スキーマが既にWindows LAPS拡張済みの場合に指定するとスキップする。

.PARAMETER LapsPasswordComplexity
    LAPSパスワードの複雑さ (1-8)。既定 4(大文字+小文字+数字+特殊文字)。
    5-8はパスフレーズ系でWindows 11 24H2/Windows Server 2025以降のみ対応。

.PARAMETER LapsPostAuthenticationActions
    認証後アクション。1=パスワードリセットのみ, 3=リセット+サインアウト(既定),
    5=リセット+再起動, 11=リセット+サインアウト+残存プロセス終了(24H2以降のみ)。

.PARAMETER LapsPostAuthenticationResetDelay
    認証後アクションを実行するまでの猶予時間(0-24時間)。既定24時間。0で無効化。

.PARAMETER LapsAdministratorAccountName
    管理対象ローカル管理者アカウント名。未指定(既定)では組み込みAdministratorを管理する。

.PARAMETER LapsPasswordExpirationProtectionEnabled
    パスワード最大有効期間の強制を有効にするか。既定 $true。

.PARAMETER LapsADPasswordEncryptionEnabled
    Active Directoryに保存するパスワードの暗号化を有効にするか。既定 $true。
    有効化にはドメイン機能レベル2016以降が必要。

.PARAMETER LapsADPasswordEncryptionPrincipal
    AD上の暗号化パスワードを復号できるユーザー/グループ(SIDまたは完全修飾名)。
    未指定では既定でDomain Adminsのみが復号可能。

.PARAMETER LapsADEncryptedPasswordHistorySize
    ADに保持する暗号化パスワード履歴の世代数(0-12)。既定0(無効)。

.PARAMETER LapsADBackupDSRMPassword
    ドメインコントローラーのDSRMアカウントパスワードもADにバックアップするか。
    ドメインコントローラーが対象OUに含まれる場合のみ意味を持つ。既定オフ。

.PARAMETER AsrRuleModes
    ASRルールGUID→モード('Audit','Block','Warn','Disabled')のハッシュテーブル。
    指定したルールは AsrAction より優先される。未指定のルールは AsrAction の値を使う。

.PARAMETER ExclusionPaths
    ASR除外パスの一覧(全ルール共通の除外。Defenderの仕様上ルール単位の除外は不可)。
    ワイルドカードや環境変数を含められる。

.NOTES
    Version: 1.2.0

.EXAMPLE
    .\Configure-SecurityBaseline.ps1 -TargetOU "OU=Workstations,DC=contoso,DC=com" -WhatIf

.EXAMPLE
    .\Configure-SecurityBaseline.ps1 -TargetOU "OU=Workstations,DC=contoso,DC=com"

.EXAMPLE
    # 監査ログ確認後、ASRをブロックモードに切替
    .\Configure-SecurityBaseline.ps1 -TargetOU "OU=Workstations,DC=contoso,DC=com" -AsrAction Block -SkipSchemaExtension
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$TargetOU,

    [string]$GPOName = 'Security-Baseline-LAPS-LSA-ASR',

    [ValidateSet('Audit', 'Block')]
    [string]$AsrAction = 'Audit',

    [switch]$LsaProtectionUefiLock,

    [ValidateRange(14, 64)]
    [int]$LapsPasswordLength = 20,

    [ValidateRange(1, 365)]
    [int]$LapsPasswordAgeDays = 30,

    [switch]$SkipSchemaExtension,

    [ValidateRange(1, 8)]
    [int]$LapsPasswordComplexity = 4,

    [ValidateSet(1, 3, 5, 11)]
    [int]$LapsPostAuthenticationActions = 3,

    [ValidateRange(0, 24)]
    [int]$LapsPostAuthenticationResetDelay = 24,

    [string]$LapsAdministratorAccountName,

    [bool]$LapsPasswordExpirationProtectionEnabled = $true,

    [bool]$LapsADPasswordEncryptionEnabled = $true,

    [string]$LapsADPasswordEncryptionPrincipal,

    [ValidateRange(0, 12)]
    [int]$LapsADEncryptedPasswordHistorySize = 0,

    [switch]$LapsADBackupDSRMPassword,

    [hashtable]$AsrRuleModes,

    [string[]]$ExclusionPaths
)

$ErrorActionPreference = 'Stop'

foreach ($m in 'ActiveDirectory', 'GroupPolicy', 'LAPS') {
    if (-not (Get-Module -ListAvailable -Name $m)) {
        throw "必須モジュール '$m' が見つかりません。RSAT / Windows LAPS PowerShell モジュールをインストールしてください。"
    }
    Import-Module $m -ErrorAction Stop
}

try {
    $null = Get-ADOrganizationalUnit -Identity $TargetOU
} catch {
    throw "TargetOU '$TargetOU' が見つかりません: $_"
}

# ── 1. Windows LAPS ──────────────────────────────────────────────
if (-not $SkipSchemaExtension) {
    $schemaExtended = [bool](Get-ADObject -SearchBase (Get-ADRootDSE).schemaNamingContext -Filter "name -eq 'ms-LAPS-Password'" -ErrorAction SilentlyContinue)
    if (-not $schemaExtended) {
        if ($PSCmdlet.ShouldProcess('Active Directory Schema', 'Windows LAPS 属性を追加 (Update-LapsADSchema)')) {
            Update-LapsADSchema -Confirm:$false
        }
    } else {
        Write-Verbose 'Windows LAPS スキーマは拡張済みのためスキップします。'
    }
}

if ($PSCmdlet.ShouldProcess($TargetOU, 'LAPSがコンピューターに自身のパスワードを書き込む権限を委任')) {
    Set-LapsADComputerSelfPermission -Identity $TargetOU
}

# ── GPO 作成 / 取得 ───────────────────────────────────────────────
$gpo = Get-GPO -Name $GPOName -ErrorAction SilentlyContinue
if (-not $gpo) {
    if ($PSCmdlet.ShouldProcess($GPOName, 'GPO を新規作成')) {
        $gpo = New-GPO -Name $GPOName -Comment 'LAPS / LSA Protection / ASR baseline'
    }
}

if ($PSCmdlet.ShouldProcess("$TargetOU <- $GPOName", 'GPO をリンク')) {
    $existingLink = (Get-GPInheritance -Target $TargetOU).GpoLinks | Where-Object { $_.DisplayName -eq $GPOName }
    if (-not $existingLink) {
        New-GPLink -Name $GPOName -Target $TargetOU -LinkEnabled Yes | Out-Null
    }
}

# Windows LAPSのGPOポリシールート(Windows LAPS ADMXテンプレートが参照するキー)。
# CSP用ルート(HKLM\Software\Microsoft\Policies\LAPS)と誤りやすいので注意。
$lapsPolicyKey = 'HKLM\Software\Microsoft\Windows\CurrentVersion\Policies\LAPS'
$lapsDwordSettings = @{
    BackupDirectory                     = 2   # 2 = Active Directory に保存(Entra IDではなく)
    PasswordComplexity                  = $LapsPasswordComplexity
    PasswordLength                      = $LapsPasswordLength
    PasswordAgeDays                     = $LapsPasswordAgeDays
    PostAuthenticationActions           = $LapsPostAuthenticationActions
    PostAuthenticationResetDelay        = $LapsPostAuthenticationResetDelay
    PasswordExpirationProtectionEnabled = [int]$LapsPasswordExpirationProtectionEnabled
    ADPasswordEncryptionEnabled         = [int]$LapsADPasswordEncryptionEnabled
    ADEncryptedPasswordHistorySize      = $LapsADEncryptedPasswordHistorySize
    ADBackupDSRMPassword                = [int]$LapsADBackupDSRMPassword.IsPresent
}
foreach ($name in $lapsDwordSettings.Keys) {
    if ($PSCmdlet.ShouldProcess("$GPOName : $lapsPolicyKey\$name", "値を $($lapsDwordSettings[$name]) に設定")) {
        Set-GPRegistryValue -Name $GPOName -Key $lapsPolicyKey -ValueName $name -Type DWord -Value $lapsDwordSettings[$name] | Out-Null
    }
}

$lapsStringSettings = @{
    AdministratorAccountName      = $LapsAdministratorAccountName
    ADPasswordEncryptionPrincipal = $LapsADPasswordEncryptionPrincipal
}
foreach ($name in $lapsStringSettings.Keys) {
    if (-not [string]::IsNullOrWhiteSpace($lapsStringSettings[$name])) {
        if ($PSCmdlet.ShouldProcess("$GPOName : $lapsPolicyKey\$name", "値を '$($lapsStringSettings[$name])' に設定")) {
            Set-GPRegistryValue -Name $GPOName -Key $lapsPolicyKey -ValueName $name -Type String -Value $lapsStringSettings[$name] | Out-Null
        }
    }
}

# ── 2. LSA Protection (RunAsPPL) ─────────────────────────────────
$lsaKey = 'HKLM\SYSTEM\CurrentControlSet\Control\Lsa'
$runAsPPLValue = if ($LsaProtectionUefiLock) { 2 } else { 1 }
if ($PSCmdlet.ShouldProcess("$GPOName : $lsaKey\RunAsPPL", "値を $runAsPPLValue に設定")) {
    Set-GPRegistryValue -Name $GPOName -Key $lsaKey -ValueName 'RunAsPPL' -Type DWord -Value $runAsPPLValue | Out-Null
}

# ── 3. ASR ルール (Microsoft 推奨セット) ──────────────────────────
# 値: 0=無効 1=ブロック 2=監査 6=警告
$asrModeToValue = @{ Disabled = 0; Block = 1; Audit = 2; Warn = 6 }
$defaultAsrValue = $asrModeToValue[$AsrAction]
$asrRules = @{
    '56a863a9-875e-4185-98a7-b882c64b5ce5' = 'Block abuse of exploited vulnerable signed drivers'
    '7674ba52-37eb-4a4f-a9a1-f0f9a1619a2c' = 'Block Adobe Reader from creating child processes'
    'd4f940ab-401b-4efc-aadc-ad5f3c50688a' = 'Block all Office applications from creating child processes'
    '9e6c4e1f-7d60-472f-ba1a-a39ef669e4b2' = 'Block credential stealing from lsass.exe'
    'be9ba2d9-53ea-4cdc-84e5-9b1eeee46550' = 'Block executable content from email client and webmail'
    '01443614-cd74-433a-b99e-2ecdc07bfc25' = 'Block executable files unless prevalence/age/trusted list criteria met'
    '5beb7efe-fd9a-4556-801d-275e5ffc04cc' = 'Block execution of potentially obfuscated scripts'
    'd3e037e1-3eb8-44c8-a917-57927947596d' = 'Block JavaScript/VBScript from launching downloaded executable content'
    '3b576869-a4ec-4529-8536-b80a7769e899' = 'Block Office applications from creating executable content'
    '75668c1f-73b5-4cf0-bb93-3ecf5cb7cc84' = 'Block Office applications from injecting code into other processes'
    '26190899-1602-49e8-8b27-eb1d0a1ce869' = 'Block Office communication application from creating child processes'
    'e6db77e5-3df2-4cf1-b95a-636979351e5b' = 'Block persistence through WMI event subscription'
    'd1e49aac-8f56-4280-b9ba-993a6d77406c' = 'Block process creations from PSExec and WMI commands'
    'b2b3f03d-6a65-4f7b-a9c7-1c7ef74a9ba4' = 'Block untrusted and unsigned processes that run from USB'
    '92e97fa1-2edf-4476-bdd6-9dd0b4dddc7b' = 'Block Win32 API calls from Office macros'
    'c1db55ab-c21a-4637-bb3f-a12568109d35' = 'Use advanced protection against ransomware'
}

$asrKey = 'HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Windows Defender Exploit Guard\ASR\Rules'
if ($PSCmdlet.ShouldProcess("$GPOName : ASR", "$($asrRules.Count) 個のルールを個別モードで設定 (既定モード '$AsrAction')")) {
    Set-GPRegistryValue -Name $GPOName -Key 'HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Windows Defender Exploit Guard\ASR' -ValueName 'ExploitGuard_ASR_Rules' -Type DWord -Value 1 | Out-Null
    foreach ($guid in $asrRules.Keys) {
        $modeName = $AsrAction
        if ($AsrRuleModes -and $AsrRuleModes.ContainsKey($guid)) {
            $modeName = $AsrRuleModes[$guid]
        }
        if (-not $asrModeToValue.ContainsKey($modeName)) {
            throw "AsrRuleModes['$guid'] の値 '$modeName' が不正です。'Audit','Block','Warn','Disabled' のいずれかを指定してください。"
        }
        Set-GPRegistryValue -Name $GPOName -Key $asrKey -ValueName $guid -Type String -Value $asrModeToValue[$modeName] | Out-Null
    }
}

# ── ASR 除外パス (全ルール共通) ────────────────────────────────
if ($ExclusionPaths -and $ExclusionPaths.Count -gt 0) {
    $exclusionKey = 'HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Windows Defender Exploit Guard\ASR\ASROnlyExclusions'
    if ($PSCmdlet.ShouldProcess("$GPOName : ASR除外", "$($ExclusionPaths.Count) 件の除外パスを設定")) {
        foreach ($path in $ExclusionPaths) {
            Set-GPRegistryValue -Name $GPOName -Key $exclusionKey -ValueName $path -Type String -Value 0 | Out-Null
        }
    }
}

Write-Host "完了: GPO '$GPOName' に LAPS / LSA Protection / ASR を設定し、'$TargetOU' にリンクしました。" -ForegroundColor Green
if (-not $AsrRuleModes -and $AsrAction -eq 'Audit') {
    Write-Host "ASRは監査モードです。イベントビューアー (Microsoft-Windows-Windows Defender/Operational, ID 1121) でブロック候補を確認後、-AsrAction Block -SkipSchemaExtension で再実行してください。" -ForegroundColor Yellow
}
