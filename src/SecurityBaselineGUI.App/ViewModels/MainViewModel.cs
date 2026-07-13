using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecurityBaselineGUI.Core.Models;
using SecurityBaselineGUI.Core.Services;

namespace SecurityBaselineGUI.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly PowerShellExecutionService _executionService;
    private readonly HistoryStore _historyStore;
    private readonly ProfileStore _profileStore;
    private readonly CsvExportService _csvExportService;
    private readonly CsvColumnPreferenceStore _csvColumnPreferenceStore;
    private readonly ScriptRepositoryService _scriptRepositoryService;
    private readonly BackupSettingsStore _backupSettingsStore;
    private readonly DatabaseBackupService _databaseBackupService;
    private readonly LapsSchemaStatusService _lapsSchemaStatusService;
    private readonly SelectedOuStore _selectedOuStore;
    private readonly AppPaths _paths;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _backupTimer;

    public MainViewModel(
        PowerShellExecutionService executionService,
        HistoryStore historyStore,
        ProfileStore profileStore,
        CsvExportService csvExportService,
        CsvColumnPreferenceStore csvColumnPreferenceStore,
        ScriptRepositoryService scriptRepositoryService,
        BackupSettingsStore backupSettingsStore,
        DatabaseBackupService databaseBackupService,
        LapsSchemaStatusService lapsSchemaStatusService,
        SelectedOuStore selectedOuStore,
        AppPaths paths)
    {
        _executionService = executionService;
        _historyStore = historyStore;
        _profileStore = profileStore;
        _csvExportService = csvExportService;
        _csvColumnPreferenceStore = csvColumnPreferenceStore;
        _scriptRepositoryService = scriptRepositoryService;
        _backupSettingsStore = backupSettingsStore;
        _databaseBackupService = databaseBackupService;
        _lapsSchemaStatusService = lapsSchemaStatusService;
        _selectedOuStore = selectedOuStore;
        _paths = paths;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _executionService.LogReceived += OnLogReceived;

        AsrRules = new ObservableCollection<AsrRuleRowViewModel>(
            AsrRuleCatalog.Rules.Select(r => new AsrRuleRowViewModel
            {
                Guid = r.Guid,
                Name = r.Name,
                Description = r.Description,
            }));

        _ = RefreshHistoryAsync();
        _ = RefreshProfilesAsync();
        _ = RefreshLapsSchemaStatusAsync();
        RefreshScriptInfo();

        var backupSettings = _backupSettingsStore.Load();
        BackupEnabled = backupSettings.Enabled;
        BackupIntervalHours = backupSettings.IntervalHours;
        BackupDestination = backupSettings.DestinationDirectory ?? string.Empty;
        LastBackupText = FormatLastBackupText(backupSettings.LastBackupUtc);

        // 定期バックアップは、アプリが起動している間だけ15分おきに要否を判定する
        // (常駐サービスではないため、アプリが起動していない間は実行されない点に注意)。
        _backupTimer = new DispatcherTimer(TimeSpan.FromMinutes(15), DispatcherPriority.Background, OnBackupTimerTick, _dispatcher);
        if (!BackupEnabled)
        {
            _backupTimer.Stop();
        }
    }

    // ── 基本タブ(OU選択自体は左の独立ウィンドウ OuSelectorWindow で行う) ──
    /// <summary>現在作業中のOU一覧。OuSelectorWindowと共有する状態を読み取り専用で参照する。</summary>
    public ObservableCollection<SelectedOuEntry> SelectedOus => _selectedOuStore.SelectedOus;

    public string GpoBaseName
    {
        get => _selectedOuStore.GpoBaseName;
        set
        {
            if (_selectedOuStore.GpoBaseName != value)
            {
                _selectedOuStore.GpoBaseName = value;
                OnPropertyChanged();
            }
        }
    }

    // ── LAPSタブ ──────────────────────────────────────────
    [ObservableProperty] private int _lapsPasswordLength = 20;
    [ObservableProperty] private int _lapsPasswordAgeDays = 30;

    /// <summary>true: スキーマ拡張済み(チェックボックスはグレーアウトし、拡張はスキップ)。</summary>
    [ObservableProperty] private bool _isLapsSchemaExtended;
    [ObservableProperty] private bool _isLapsSchemaStatusChecking = true;
    [ObservableProperty] private string _lapsSchemaStatusMessage = "確認中...";

    /// <summary>ユーザーが「スキーマを拡張する」を選択したか(スキーマ拡張済みなら無効化・強制false)。</summary>
    [ObservableProperty] private bool _performSchemaExtension;

    [ObservableProperty] private LabeledOption<int> _lapsPasswordComplexity = PasswordComplexityOptions[3];
    [ObservableProperty] private LabeledOption<int> _lapsPostAuthenticationActions = PostAuthenticationActionOptions[1];
    [ObservableProperty] private int _lapsPostAuthenticationResetDelay = 24;
    [ObservableProperty] private string _lapsAdministratorAccountName = string.Empty;
    [ObservableProperty] private bool _lapsPasswordExpirationProtectionEnabled = true;
    [ObservableProperty] private bool _lapsADPasswordEncryptionEnabled = true;
    [ObservableProperty] private string _lapsADPasswordEncryptionPrincipal = string.Empty;
    [ObservableProperty] private int _lapsADEncryptedPasswordHistorySize = 0;
    [ObservableProperty] private bool _lapsADBackupDSRMPassword;

    public static IReadOnlyList<LabeledOption<int>> PasswordComplexityOptions { get; } =
    [
        new(1, "1: 大文字のみ"),
        new(2, "2: 大文字+小文字"),
        new(3, "3: 大文字+小文字+数字"),
        new(4, "4: 大文字+小文字+数字+特殊文字(既定・推奨)"),
        new(5, "5: 4+読みやすさ改善(24H2以降)"),
        new(6, "6: パスフレーズ・長い単語(24H2以降)"),
        new(7, "7: パスフレーズ・短い単語(24H2以降)"),
        new(8, "8: パスフレーズ・一意プレフィックス付き短い単語(24H2以降)"),
    ];

    public static IReadOnlyList<LabeledOption<int>> PostAuthenticationActionOptions { get; } =
    [
        new(1, "1: パスワードのリセットのみ"),
        new(3, "3: リセット+サインアウト(既定)"),
        new(5, "5: リセット+再起動"),
        new(11, "11: リセット+サインアウト+残存プロセス終了(24H2以降)"),
    ];

    [RelayCommand]
    private async Task RefreshLapsSchemaStatusAsync()
    {
        IsLapsSchemaStatusChecking = true;
        try
        {
            var extended = await Task.Run(_lapsSchemaStatusService.IsSchemaExtended);
            IsLapsSchemaExtended = extended;
            LapsSchemaStatusMessage = extended ? "拡張済みです。" : "未拡張です。実行時にスキーマを拡張できます。";
            if (extended)
            {
                PerformSchemaExtension = false;
            }
        }
        catch (AdOuBrowserException ex)
        {
            IsLapsSchemaExtended = false;
            LapsSchemaStatusMessage = $"確認できませんでした: {ex.Message}";
        }
        finally
        {
            IsLapsSchemaStatusChecking = false;
        }
    }

    // ── LSA Protectionタブ ────────────────────────────────
    [ObservableProperty] private bool _lsaProtectionUefiLock;

    // ── ASRタブ ───────────────────────────────────────────
    [ObservableProperty] private AsrRuleMode _bulkAsrMode = AsrRuleMode.Audit;
    public ObservableCollection<AsrRuleRowViewModel> AsrRules { get; }
    public IReadOnlyList<AsrRuleMode> AsrModes { get; } = AsrRuleRowViewModel.AvailableModes;

    [RelayCommand]
    private void ApplyBulkAsrMode()
    {
        foreach (var rule in AsrRules)
        {
            rule.IsEnabled = true;
            rule.Mode = BulkAsrMode;
        }
    }

    // ── 除外設定タブ ──────────────────────────────────────
    public ObservableCollection<string> ExclusionPaths { get; } = [];
    [ObservableProperty] private string _newExclusionPath = string.Empty;

    [RelayCommand]
    private void AddExclusionPath()
    {
        var path = NewExclusionPath.Trim();
        if (path.Length == 0 || ExclusionPaths.Contains(path))
        {
            return;
        }
        ExclusionPaths.Add(path);
        NewExclusionPath = string.Empty;
    }

    [RelayCommand]
    private void RemoveExclusionPath(string path) => ExclusionPaths.Remove(path);

    // ── 実行・ログ ────────────────────────────────────────
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _logText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    [RelayCommand]
    private Task PreviewAsync() => ExecuteAsync(whatIf: true);

    [RelayCommand]
    private Task RunAsync() => ExecuteAsync(whatIf: false);

    /// <summary>選択中の全OUに対して順番にps1を実行する。GPO名はOUごとに「ベース名-OU名」で決まる。</summary>
    private async Task ExecuteAsync(bool whatIf)
    {
        var targets = SelectedOus.ToList();
        if (targets.Count == 0)
        {
            StatusMessage = "左のウィンドウで対象OUを1つ以上選択してください。";
            return;
        }

        IsBusy = true;
        LogText = string.Empty;
        StatusMessage = whatIf ? "プレビュー実行中..." : "実行中...";

        var asrRuleModes = AsrRules.ToDictionary(r => r.Guid, r => r.EffectiveMode.ToString());
        var exclusionPathsArray = ExclusionPaths.ToArray();
        var succeededCount = 0;

        foreach (var target in targets)
        {
            var gpoName = _selectedOuStore.ComputeGpoName(target);
            AppendLog("Host", $"── OU: {target.DistinguishedName} (GPO: {gpoName}) ──");

            var parameters = new Dictionary<string, object?>
            {
                ["TargetOU"] = target.DistinguishedName,
                ["GPOName"] = gpoName,
                ["LapsPasswordLength"] = LapsPasswordLength,
                ["LapsPasswordAgeDays"] = LapsPasswordAgeDays,
                ["SkipSchemaExtension"] = IsLapsSchemaExtended || !PerformSchemaExtension,
                ["LapsPasswordComplexity"] = LapsPasswordComplexity.Value,
                ["LapsPostAuthenticationActions"] = LapsPostAuthenticationActions.Value,
                ["LapsPostAuthenticationResetDelay"] = LapsPostAuthenticationResetDelay,
                ["LapsAdministratorAccountName"] = string.IsNullOrWhiteSpace(LapsAdministratorAccountName) ? null : LapsAdministratorAccountName,
                ["LapsPasswordExpirationProtectionEnabled"] = LapsPasswordExpirationProtectionEnabled,
                ["LapsADPasswordEncryptionEnabled"] = LapsADPasswordEncryptionEnabled,
                ["LapsADPasswordEncryptionPrincipal"] = string.IsNullOrWhiteSpace(LapsADPasswordEncryptionPrincipal) ? null : LapsADPasswordEncryptionPrincipal,
                ["LapsADEncryptedPasswordHistorySize"] = LapsADEncryptedPasswordHistorySize,
                ["LapsADBackupDSRMPassword"] = LapsADBackupDSRMPassword,
                ["LsaProtectionUefiLock"] = LsaProtectionUefiLock,
                ["AsrAction"] = BulkAsrMode == AsrRuleMode.Block ? "Block" : "Audit",
                ["AsrRuleModes"] = asrRuleModes,
                ["ExclusionPaths"] = exclusionPathsArray,
            };

            var request = new ExecutionRequest
            {
                ScriptPath = _paths.ScriptPath,
                Parameters = parameters,
                WhatIf = whatIf,
            };

            var result = await _executionService.RunAsync(request);
            if (result.Succeeded)
            {
                succeededCount++;
            }

            await _historyStore.InsertAsync(new ExecutionHistoryEntry
            {
                Timestamp = DateTimeOffset.Now,
                UserName = Environment.UserName,
                TargetOU = target.DistinguishedName,
                GPOName = gpoName,
                AsrRuleModesJson = JsonSerializer.Serialize(asrRuleModes),
                ExclusionPathsJson = JsonSerializer.Serialize(exclusionPathsArray),
                Succeeded = result.Succeeded,
                DurationMs = (long)result.Duration.TotalMilliseconds,
                LogText = result.LogText,
                WasWhatIf = whatIf,
            });
        }

        StatusMessage = $"完了: {targets.Count}件中{succeededCount}件成功";
        IsBusy = false;
        await RefreshHistoryAsync();
        if (!IsLapsSchemaStatusChecking)
        {
            _ = RefreshLapsSchemaStatusAsync();
        }
    }

    private void AppendLog(string level, string message)
    {
        _dispatcher.Invoke(() => LogText += $"[{DateTimeOffset.Now:HH:mm:ss}] [{level}] {message}{Environment.NewLine}");
    }

    private void OnLogReceived(object? sender, ExecutionLogLine line) => AppendLog(line.Level, line.Message);

    // ── 履歴タブ ──────────────────────────────────────────
    public ObservableCollection<ExecutionHistoryEntry> History { get; } = [];

    [RelayCommand]
    private async Task RefreshHistoryAsync()
    {
        var entries = await _historyStore.GetRecentAsync();
        History.Clear();
        foreach (var entry in entries)
        {
            History.Add(entry);
        }
    }

    /// <summary>現在表示中の履歴を、保存済みの列カスタマイズ設定(未設定なら既定列セット)でCSV化する。</summary>
    public async Task<string> BuildHistoryCsvAsync()
    {
        var saved = await _csvColumnPreferenceStore.GetAllAsync();
        IReadOnlyList<string> orderedVisibleKeys = saved.Count > 0
            ? saved.Where(p => p.Visible).OrderBy(p => p.DisplayOrder).Select(p => p.ColumnKey).ToList()
            : CsvColumnCatalog.AllColumns.Select(c => c.Key).ToList();

        return _csvExportService.BuildCsv(History, orderedVisibleKeys);
    }

    // ── プロファイル管理 ──────────────────────────────────
    // 対象OU自体はOuSelectorWindowで常時選択するセッション情報のため、プロファイルには含めない。
    public ObservableCollection<string> ProfileNames { get; } = [];
    [ObservableProperty] private string _profileName = string.Empty;

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            return;
        }

        var payload = new
        {
            GpoBaseName,
            LapsPasswordLength,
            LapsPasswordAgeDays,
            LapsPasswordComplexity = LapsPasswordComplexity.Value,
            LapsPostAuthenticationActions = LapsPostAuthenticationActions.Value,
            LapsPostAuthenticationResetDelay,
            LapsAdministratorAccountName,
            LapsPasswordExpirationProtectionEnabled,
            LapsADPasswordEncryptionEnabled,
            LapsADPasswordEncryptionPrincipal,
            LapsADEncryptedPasswordHistorySize,
            LapsADBackupDSRMPassword,
            LsaProtectionUefiLock,
            AsrRules = AsrRules.Select(r => new { r.Guid, Mode = r.Mode.ToString(), r.IsEnabled }),
            ExclusionPaths = ExclusionPaths.ToArray(),
        };

        await _profileStore.SaveAsync(ProfileName, JsonSerializer.Serialize(payload));
        await RefreshProfilesAsync();
    }

    [RelayCommand]
    private async Task LoadProfileAsync(string name)
    {
        var profiles = await _profileStore.GetAllAsync();
        var profile = profiles.FirstOrDefault(p => p.Name == name);
        if (profile is null)
        {
            return;
        }

        using var doc = JsonDocument.Parse(profile.ParametersJson);
        var root = doc.RootElement;
        GpoBaseName = root.GetProperty(nameof(GpoBaseName)).GetString() ?? GpoBaseName;
        LapsPasswordLength = root.GetProperty(nameof(LapsPasswordLength)).GetInt32();
        LapsPasswordAgeDays = root.GetProperty(nameof(LapsPasswordAgeDays)).GetInt32();
        LapsPasswordComplexity = FindOption(PasswordComplexityOptions, root.GetProperty(nameof(LapsPasswordComplexity)).GetInt32());
        LapsPostAuthenticationActions = FindOption(PostAuthenticationActionOptions, root.GetProperty(nameof(LapsPostAuthenticationActions)).GetInt32());
        LapsPostAuthenticationResetDelay = root.GetProperty(nameof(LapsPostAuthenticationResetDelay)).GetInt32();
        LapsAdministratorAccountName = root.GetProperty(nameof(LapsAdministratorAccountName)).GetString() ?? string.Empty;
        LapsPasswordExpirationProtectionEnabled = root.GetProperty(nameof(LapsPasswordExpirationProtectionEnabled)).GetBoolean();
        LapsADPasswordEncryptionEnabled = root.GetProperty(nameof(LapsADPasswordEncryptionEnabled)).GetBoolean();
        LapsADPasswordEncryptionPrincipal = root.GetProperty(nameof(LapsADPasswordEncryptionPrincipal)).GetString() ?? string.Empty;
        LapsADEncryptedPasswordHistorySize = root.GetProperty(nameof(LapsADEncryptedPasswordHistorySize)).GetInt32();
        LapsADBackupDSRMPassword = root.GetProperty(nameof(LapsADBackupDSRMPassword)).GetBoolean();
        LsaProtectionUefiLock = root.GetProperty(nameof(LsaProtectionUefiLock)).GetBoolean();

        ExclusionPaths.Clear();
        foreach (var path in root.GetProperty(nameof(ExclusionPaths)).EnumerateArray())
        {
            ExclusionPaths.Add(path.GetString() ?? string.Empty);
        }

        var rulesByGuid = AsrRules.ToDictionary(r => r.Guid);
        foreach (var ruleJson in root.GetProperty("AsrRules").EnumerateArray())
        {
            var guid = ruleJson.GetProperty("Guid").GetString();
            if (guid is not null && rulesByGuid.TryGetValue(guid, out var rule))
            {
                rule.IsEnabled = ruleJson.GetProperty("IsEnabled").GetBoolean();
                rule.Mode = Enum.Parse<AsrRuleMode>(ruleJson.GetProperty("Mode").GetString()!);
            }
        }
    }

    private static LabeledOption<int> FindOption(IReadOnlyList<LabeledOption<int>> options, int value) =>
        options.FirstOrDefault(o => o.Value == value) ?? options[0];

    private async Task RefreshProfilesAsync()
    {
        var profiles = await _profileStore.GetAllAsync();
        ProfileNames.Clear();
        foreach (var profile in profiles)
        {
            ProfileNames.Add(profile.Name);
        }
    }

    // ── スクリプト管理タブ ────────────────────────────────
    [ObservableProperty] private string _scriptVersion = string.Empty;
    [ObservableProperty] private string _scriptSha256 = string.Empty;
    [ObservableProperty] private string _scriptLastModified = string.Empty;
    public string ScriptPath => _paths.ScriptPath;

    public void RefreshScriptInfo()
    {
        var info = _scriptRepositoryService.GetVersionInfo(_paths.ScriptPath);
        ScriptVersion = info.Version ?? "(不明)";
        ScriptSha256 = info.Sha256;
        ScriptLastModified = info.LastModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }

    // ── バックアップタブ ──────────────────────────────────
    [ObservableProperty] private bool _backupEnabled;
    [ObservableProperty] private int _backupIntervalHours = 24;
    [ObservableProperty] private string _backupDestination = string.Empty;
    [ObservableProperty] private string _lastBackupText = "(未実行)";

    [RelayCommand]
    private void SaveBackupSettings()
    {
        var settings = _backupSettingsStore.Load();
        settings.Enabled = BackupEnabled;
        settings.IntervalHours = Math.Max(1, BackupIntervalHours);
        settings.DestinationDirectory = string.IsNullOrWhiteSpace(BackupDestination) ? null : BackupDestination;
        _backupSettingsStore.Save(settings);

        if (BackupEnabled)
        {
            _backupTimer.Interval = TimeSpan.FromMinutes(15);
            _backupTimer.Start();
        }
        else
        {
            _backupTimer.Stop();
        }
    }

    [RelayCommand]
    private void BackupNow()
    {
        if (string.IsNullOrWhiteSpace(BackupDestination))
        {
            LastBackupText = "バックアップ先フォルダーを指定してください。";
            return;
        }

        try
        {
            var result = _databaseBackupService.CreateBackup(_paths.DatabasePath, _paths.DatabaseKeyPath, BackupDestination);

            var settings = _backupSettingsStore.Load();
            settings.Enabled = BackupEnabled;
            settings.IntervalHours = Math.Max(1, BackupIntervalHours);
            settings.DestinationDirectory = BackupDestination;
            settings.LastBackupUtc = result.Timestamp;
            _backupSettingsStore.Save(settings);

            LastBackupText = FormatLastBackupText(result.Timestamp);
        }
        catch (Exception ex)
        {
            LastBackupText = $"失敗: {ex.Message}";
        }
    }

    /// <summary>復元前に現行DBの安全バックアップを取ってから、選択されたバックアップで上書きする。</summary>
    public void RestoreBackup(string backupDbPath, string backupKeyPath)
    {
        var preRestoreDir = Path.Combine(Path.GetDirectoryName(_paths.DatabasePath)!, "PreRestoreBackup");
        _databaseBackupService.CreateBackup(_paths.DatabasePath, _paths.DatabaseKeyPath, preRestoreDir);
        _databaseBackupService.RestoreBackup(backupDbPath, backupKeyPath, _paths.DatabasePath, _paths.DatabaseKeyPath);
    }

    private void OnBackupTimerTick(object? sender, EventArgs e)
    {
        var settings = _backupSettingsStore.Load();
        if (BackupSchedule.IsDue(settings, DateTimeOffset.Now))
        {
            BackupNow();
        }
    }

    private static string FormatLastBackupText(DateTimeOffset? lastBackupUtc) =>
        lastBackupUtc is null
            ? "(未実行)"
            : $"{lastBackupUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss} に完了";
}
