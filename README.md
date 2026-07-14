# SecurityBaselineGUI

Windows LAPS、LSA Protection、Microsoft Defender ASR ルールを、Active Directory の OU 単位で GPO 経由展開するための WPF 管理ツール。

PowerShell スクリプトを直接手作業で実行するのではなく、対象 OU の選択、パラメータ入力、WhatIf プレビュー、実行履歴、CSV 出力、設定プロファイル、スクリプト差し替え、暗号化されたローカル履歴DBを GUI から扱えるようにする。

## 目的

- AD ドメイン環境で、端末向けセキュリティベースラインを OU 単位で展開する。
- Windows LAPS、LSA Protection、ASR を同じ GPO 展開フローで設定する。
- いきなりブロック設定を入れず、WhatIf と ASR 監査モードから段階的に適用する。
- 誰が、いつ、どの OU に、どの設定で実行したかを履歴として残す。
- スクリプト更新時に、バージョン、SHA256、パラメータ差分を確認してから差し替える。

## 主な機能

### OU 選択

- Active Directory の defaultNamingContext を取得し、OU ツリーを表示する。
- OU は遅延ロードで 1 階層ずつ取得する。
- 複数 OU を選択できる。
- MainWindow の左側に OU 選択ウィンドウを常時表示し、現在作業中の OU を確認できる。
- GPO 名はベース名と OU 名から OU ごとに決定する。

### Windows LAPS

- Windows LAPS スキーマ拡張状態を `ms-LAPS-Password` 属性で確認する。
- 未拡張の場合、実行時にスキーマ拡張を選択できる。
- 対象 OU に対し、コンピューター自身が LAPS パスワードを書き込む権限を委任する。
- GPO に Windows LAPS ポリシーを設定する。
- 設定できる主な項目:
  - パスワード長
  - ローテーション間隔
  - パスワード複雑性
  - 認証後アクション
  - 認証後リセット猶予時間
  - 管理対象ローカル管理者アカウント名
  - パスワード有効期限強制
  - AD パスワード暗号化
  - 復号許可プリンシパル
  - 暗号化パスワード履歴世代数
  - DSRM パスワードバックアップ

### LSA Protection

- GPO 経由で `RunAsPPL` を設定し、LSASS を PPL 化する。
- UEFI ロック付き有効化を選択できる。
- UEFI ロックは解除が難しいため、画面上で注意を表示する。

### ASR ルール

- Microsoft Defender Attack Surface Reduction ルールを GUI から設定する。
- 一括モードを Audit / Block などに適用できる。
- ルール単位で有効/無効とモードを選択できる。
- ASR 除外パスを設定できる。
- ASR 除外は Defender 仕様上、全ルール共通の除外として扱う。

### 実行

- PowerShell Runspace を都度生成して `Configure-SecurityBaseline.ps1` を実行する。
- `Verbose`、`Warning`、`Error`、`Information`、標準出力を GUI ログへ中継する。
- WhatIf プレビューを実行できる。
- 実行結果、所要時間、対象 OU、GPO 名、ASR 設定、除外設定、ログを履歴に保存する。

### 履歴と CSV

- 直近の実行履歴を表示する。
- 履歴を CSV エクスポートできる。
- CSV 出力列の表示/非表示と順序をカスタマイズできる。

### プロファイル

- GPO ベース名、LAPS、LSA Protection、ASR、除外設定をプロファイルとして保存できる。
- 保存したプロファイルを読み込んで同じ設定を再利用できる。
- 対象 OU はセッション中の選択状態として扱い、プロファイルには含めない。

### スクリプト管理

- 現在の `Configure-SecurityBaseline.ps1` のバージョン、SHA256、更新日時を表示する。
- 新しい ps1 を選択し、構文チェックとパラメータ差分確認を行う。
- 現行スクリプトを Archive フォルダーへバックアップしてから差し替える。

### ローカル DB とバックアップ

- 実行履歴、プロファイル、CSV 列設定を SQLite に保存する。
- DB は SQLCipher で暗号化する。
- 暗号化キーは DPAPI CurrentUser スコープで保護する。
- 保存先は `%LOCALAPPDATA%\SecurityBaselineGUI\Data\app.db`。
- 鍵ファイルは `%LOCALAPPDATA%\SecurityBaselineGUI\Data\app.db.key.protected`。
- DB と鍵ファイルをペアでバックアップ/復元できる。
- 定期バックアップはアプリ起動中に 15 分ごとに要否を判定する。
- 復元前には現行 DB の退避バックアップを作成する。

## 構成

```text
SecurityBaselineGUI.sln
src/
  SecurityBaselineGUI.App/
    WPF UI、ViewModel、OU選択ウィンドウ、CSV列カスタマイズ画面
  SecurityBaselineGUI.Core/
    AD参照、LAPSスキーマ確認、PowerShell実行、SQLite/SQLCipher、履歴、CSV、バックアップ
tests/
  SecurityBaselineGUI.Tests/
    xUnit テスト
```

## 主要ファイル

- `src\SecurityBaselineGUI.App\Views\MainWindow.xaml`
- `src\SecurityBaselineGUI.App\ViewModels\MainViewModel.cs`
- `src\SecurityBaselineGUI.App\ViewModels\OuSelectorViewModel.cs`
- `src\SecurityBaselineGUI.Core\Services\PowerShellExecutionService.cs`
- `src\SecurityBaselineGUI.Core\Services\ScriptRepositoryService.cs`
- `src\SecurityBaselineGUI.Core\Services\SqliteConnectionFactory.cs`
- `src\SecurityBaselineGUI.Core\Services\HistoryStore.cs`
- `src\SecurityBaselineGUI.Core\Services\AdOuBrowserService.cs`
- `src\SecurityBaselineGUI.Core\Services\LapsSchemaStatusService.cs`

## ビルド

```powershell
dotnet restore .\SecurityBaselineGUI.sln
dotnet build .\SecurityBaselineGUI.sln
```

## テスト

```powershell
dotnet test .\SecurityBaselineGUI.sln
```

確認済みの主なテスト観点:

- PowerShell スクリプトのパラメータ検出
- スクリプト差し替え時のパラメータ差分検出
- ASR ルールカタログ
- CSV 出力と列カスタマイズ
- バックアップ設定とバックアップスケジュール
- DB バックアップ
- SQLCipher 暗号化 DB の読み書き
- 誤った鍵または鍵なしで DB を読めないこと

## 実行前提

- Windows
- .NET 9
- WPF 実行環境
- AD ドメイン参加済み端末
- Active Directory へ到達できること
- GPO を作成/リンク/編集できる権限
- LAPS スキーマ拡張を行う場合は、スキーマ拡張に必要な権限
- PowerShell モジュール:
  - `ActiveDirectory`
  - `GroupPolicy`
  - `LAPS`

## 運用上の注意

- 初回は WhatIf プレビューで対象 OU、GPO 名、設定値を確認する。
- ASR はまず Audit モードで展開し、イベントログを確認してから Block へ切り替える。
- UEFI ロック付き LSA Protection は復旧が難しいため、限定環境で確認してから使う。
- DB バックアップは DB ファイルと DPAPI 保護済み鍵ファイルを必ずペアで扱う。
- 鍵は CurrentUser スコープで保護されるため、復元先のユーザー/ドメイン条件を確認する。

## ライセンスとバックアップ仕様

- 通常起動時は署名付きライセンスファイルを検証し、製品ID、有効期限、署名、許可機能が一致しない場合は起動しない。
- ライセンス公開鍵は製品本体または共通ライブラリに内蔵し、秘密鍵は製品リポジトリに置かない。
- 日次バックアップは製品本体へ組み込み、SQLCipher DB、DPAPI保護済み鍵ファイル、実行ログ、設定プロファイル、CSV列設定を暗号化して退避する。
- 日次バックアップは通常ライセンスが有効な状態で実行し、バックアップ許可コードの入力は求めない。
- 製品本体が不調な場合のみ、緊急バックアップ独立ツールでDB、鍵ファイル、ログ、設定を暗号化保全する。
- 緊急バックアップ独立ツールは、ライセンス証書に記載されたバックアップ許可コードを照合するが、通常起動、復元、DB復号、GPO展開は許可しない。

製品別の課題:

- DPAPI CurrentUser鍵を別端末で再保護する手順を、日次バックアップと緊急バックアップの両方で明確化する。
- OU DN、GPO名、実行ログには組織情報が含まれるため、バックアップmanifestには詳細を出さない。
- 既存のDBバックアップ機能と、横断仕様の暗号化 `.lsbak` 形式をどう統合するか決める。

## 未決事項

- `SecurityBaselineGUI.App.csproj` とテストプロジェクトは `..\..\..\Scripts\Configure-SecurityBaseline.ps1` を参照する。正本は `C:\Dev\Products\Scripts\Configure-SecurityBaseline.ps1` として配置済み。今後、スクリプト更新時はこの正本とアプリ出力の差し替え運用を混同しないようにする。
- `ScriptRepositoryService` のコメント上、UiSchema.json による動的 UI 生成は未構築。GUI は現行パラメータ名へ直接配線されているため、ps1 のパラメータ変更時は差分確認後に UI 側追随が必要。
- README 上の販売・導入向け説明は未整備。顧客向け資料にする場合は、前提権限、段階適用手順、監査証跡サンプル、復旧手順を別文書化する。
