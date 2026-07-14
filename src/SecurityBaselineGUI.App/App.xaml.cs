using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SecurityBaselineGUI.App.ViewModels;
using SecurityBaselineGUI.App.Views;
using SecurityBaselineGUI.Core.Services;

namespace SecurityBaselineGUI.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SecurityBaselineGUI");
        var dbPath = Path.Combine(appDataDir, "Data", "app.db");
        var protectedKeyPath = Path.Combine(appDataDir, "Data", "app.db.key.protected");
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "Configure-SecurityBaseline.ps1");
        var backupSettingsPath = Path.Combine(appDataDir, "backup-settings.json");
        var paths = new AppPaths(scriptPath, dbPath, protectedKeyPath, backupSettingsPath);

        // 仕様書7.2: DB暗号化キーはDPAPI(CurrentUserスコープ)で保護されたものをロード/生成する。
        var encryptionKey = new DpapiKeyProtector().LoadOrCreateProtectedKey(protectedKeyPath);

        var services = new ServiceCollection();
        services.AddSingleton<PowerShellExecutionService>();
        services.AddSingleton(new SqliteConnectionFactory(dbPath, encryptionKey));
        services.AddSingleton<HistoryStore>();
        services.AddSingleton<ProfileStore>();
        services.AddSingleton<ScriptParameterService>();
        services.AddSingleton<CsvExportService>();
        services.AddSingleton<CsvColumnPreferenceStore>();
        services.AddSingleton<ScriptRepositoryService>();
        services.AddSingleton<DatabaseBackupService>();
        services.AddSingleton(new BackupSettingsStore(backupSettingsPath));
        services.AddSingleton<AdOuBrowserService>();
        services.AddSingleton<LapsSchemaStatusService>();

        // 対象OUの選択状態はMainWindowとOuSelectorWindowの両方から参照される共有状態。
        services.AddSingleton<SelectedOuStore>();
        services.AddSingleton<OuSelectorViewModel>();
        services.AddSingleton<OuSelectorWindow>();

        services.AddSingleton(_ => new MainViewModel(
            _.GetRequiredService<PowerShellExecutionService>(),
            _.GetRequiredService<HistoryStore>(),
            _.GetRequiredService<ProfileStore>(),
            _.GetRequiredService<CsvExportService>(),
            _.GetRequiredService<CsvColumnPreferenceStore>(),
            _.GetRequiredService<ScriptRepositoryService>(),
            _.GetRequiredService<BackupSettingsStore>(),
            _.GetRequiredService<DatabaseBackupService>(),
            _.GetRequiredService<LapsSchemaStatusService>(),
            _.GetRequiredService<SelectedOuStore>(),
            paths));

        services.AddTransient<CsvColumnCustomizeViewModel>();
        services.AddTransient<CsvColumnCustomizeWindow>();
        services.AddSingleton<Func<CsvColumnCustomizeWindow>>(sp => () => sp.GetRequiredService<CsvColumnCustomizeWindow>());

        services.AddTransient<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        var ouSelectorWindow = _serviceProvider.GetRequiredService<OuSelectorWindow>();

        // OU選択ウィンドウはMainWindowの左側に常時表示する(仕様: 独立ウィンドウで
        // 現在作業中のOUを常時確認できるようにする)。WindowStartupLocationの既定(OS任せ)だと
        // MainWindowが画面端近くに配置され、左に並べるOU選択ウィンドウが画面外にはみ出す
        // ことがあるため、2つのウィンドウをペアとして画面中央に明示配置する。
        const double gap = 12;
        var workArea = SystemParameters.WorkArea;
        var pairWidth = ouSelectorWindow.Width + gap + mainWindow.Width;
        var pairLeft = Math.Max(workArea.Left, workArea.Left + (workArea.Width - pairWidth) / 2);

        mainWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        mainWindow.Left = pairLeft + ouSelectorWindow.Width + gap;
        mainWindow.Top = Math.Max(workArea.Top, workArea.Top + (workArea.Height - mainWindow.Height) / 2);
        mainWindow.Show(); // Ownerに設定するにはHwndの確定(Show)が先に必要

        ouSelectorWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        ouSelectorWindow.Left = pairLeft;
        ouSelectorWindow.Top = mainWindow.Top;
        ouSelectorWindow.Owner = mainWindow;
        ouSelectorWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
