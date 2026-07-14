using SecurityBaselineGUI.Core.Models;

namespace SecurityBaselineGUI.Core.Services;

/// <summary>
/// Configure-SecurityBaseline.ps1 の $asrRules と同じ16ルールを保持するカタログ。
/// ps1側のルール一覧が変わった場合はここも合わせて更新する(スキーマ検出の自動化は将来課題)。
/// </summary>
public static class AsrRuleCatalog
{
    public static readonly IReadOnlyList<AsrRuleDefinition> Rules =
    [
        new("56a863a9-875e-4185-98a7-b882c64b5ce5", "脆弱な署名済みドライバーの悪用をブロック", "Block abuse of exploited vulnerable signed drivers"),
        new("7674ba52-37eb-4a4f-a9a1-f0f9a1619a2c", "Adobe Reader の子プロセス作成をブロック", "Block Adobe Reader from creating child processes"),
        new("d4f940ab-401b-4efc-aadc-ad5f3c50688a", "Office アプリの子プロセス作成をブロック", "Block all Office applications from creating child processes"),
        new("9e6c4e1f-7d60-472f-ba1a-a39ef669e4b2", "lsass.exe からの資格情報窃取をブロック", "Block credential stealing from lsass.exe"),
        new("be9ba2d9-53ea-4cdc-84e5-9b1eeee46550", "メール/webmailからの実行可能コンテンツをブロック", "Block executable content from email client and webmail"),
        new("01443614-cd74-433a-b99e-2ecdc07bfc25", "普及度/経過期間/信頼リスト基準を満たさない実行ファイルをブロック", "Block executable files unless prevalence/age/trusted list criteria met"),
        new("5beb7efe-fd9a-4556-801d-275e5ffc04cc", "難読化された可能性のあるスクリプトの実行をブロック", "Block execution of potentially obfuscated scripts"),
        new("d3e037e1-3eb8-44c8-a917-57927947596d", "JS/VBScriptからのダウンロード実行コンテンツ起動をブロック", "Block JavaScript/VBScript from launching downloaded executable content"),
        new("3b576869-a4ec-4529-8536-b80a7769e899", "Office アプリの実行可能コンテンツ作成をブロック", "Block Office applications from creating executable content"),
        new("75668c1f-73b5-4cf0-bb93-3ecf5cb7cc84", "Office アプリから他プロセスへのコード注入をブロック", "Block Office applications from injecting code into other processes"),
        new("26190899-1602-49e8-8b27-eb1d0a1ce869", "Office通信アプリの子プロセス作成をブロック", "Block Office communication application from creating child processes"),
        new("e6db77e5-3df2-4cf1-b95a-636979351e5b", "WMIイベントサブスクリプションによる永続化をブロック", "Block persistence through WMI event subscription"),
        new("d1e49aac-8f56-4280-b9ba-993a6d77406c", "PsExec/WMIコマンドからのプロセス作成をブロック", "Block process creations from PSExec and WMI commands"),
        new("b2b3f03d-6a65-4f7b-a9c7-1c7ef74a9ba4", "USBからの未信頼・未署名プロセスをブロック", "Block untrusted and unsigned processes that run from USB"),
        new("92e97fa1-2edf-4476-bdd6-9dd0b4dddc7b", "Officeマクロからの Win32 API 呼び出しをブロック", "Block Win32 API calls from Office macros"),
        new("c1db55ab-c21a-4637-bb3f-a12568109d35", "ランサムウェアに対する高度な保護を使用", "Use advanced protection against ransomware"),
    ];
}
