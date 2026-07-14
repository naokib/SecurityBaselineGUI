namespace SecurityBaselineGUI.App;

/// <summary>App起動時に確定するファイルパス一式。MainViewModelのコンストラクタ引数肥大化を避けるための束ね役。</summary>
public sealed record AppPaths(string ScriptPath, string DatabasePath, string DatabaseKeyPath, string BackupSettingsPath);
