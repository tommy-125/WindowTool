using System.Windows;
using WindowTool.Service;

namespace WindowTool;

public partial class App : Application {
    private IProcessService? _processService;

    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);
        ThemeManager.Apply(this, ThemeManager.LoadTheme());
        _processService = new ProcessService();
        MainWindow = new MainWindow(_processService);
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e) {
        TopMostStateManager.RestoreAll();
        _processService?.Dispose();
        base.OnExit(e);
    }
}
