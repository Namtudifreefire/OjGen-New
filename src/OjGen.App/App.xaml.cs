using System;
using System.IO;
using System.Windows;
using OjGen.Core;
using OjGen.Gui.Services;

namespace OjGen.Gui;

public partial class App : Application
{
    /// <summary>Cấu hình dùng chung (trình biên dịch, timeout, thư mục output...). Lưu ở %AppData%\OjGen.</summary>
    public Settings AppSettings { get; private set; } = new();
    public string? SettingsWarning { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        // Bắt lỗi toàn cục để không "chết im lặng".
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Write("Unhandled UI dispatcher exception", args.Exception);
            try
            {
                MessageBox.Show($"Đã xảy ra lỗi và OjGen sẽ dừng để tránh tiếp tục với trạng thái không ổn định.\n\n{args.Exception.Message}\n\nNhật ký: {AppLog.FilePath}", "Lỗi hệ thống",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception) { }
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Write("Unhandled application exception; terminating=" + args.IsTerminating, args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Write("Unobserved background task exception", args.Exception);
            args.SetObserved();
        };

        try
        {
            string appPlugins = Path.Combine(AppContext.BaseDirectory, "plugins");
            string userPlugins = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OjGen", "plugins");
            foreach (string message in ProblemPluginLoader.LoadFrom(appPlugins).Concat(ProblemPluginLoader.LoadFrom(userPlugins)))
                if (!message.Contains("Không tìm thấy plugin", StringComparison.OrdinalIgnoreCase))
                    SettingsWarning = (SettingsWarning == null ? "" : SettingsWarning + Environment.NewLine) + "Plugin: " + message;
        }
        catch (Exception ex) { AppendSettingsWarning("Không nạp được plugin: " + ex.Message); AppLog.Write("Plugin startup", ex); }

        try { LoadSettings(); }
        catch (Exception ex) { AppendSettingsWarning("Không đọc được cấu hình: " + ex.Message); AppLog.Write("Settings startup", ex); }
        try { ThemeService.Initialize(); } catch (Exception ex) { AppLog.Write("Theme initialization", ex); }
        try { if (AppRegistry.GetBool("AutoThemeEnabled")) AutoThemeService.Start(); } catch (Exception ex) { AppLog.Write("Automatic theme startup", ex); }

        try
        {
            var main = new MainWindow();
            MainWindow = main;
            main.Show();
        }
        catch (Exception ex)
        {
            AppLog.Write("Main window startup", ex);
            MessageBox.Show($"Không thể khởi động ứng dụng:\n\n{ex.Message}\n\nNhật ký: {AppLog.FilePath}", "Lỗi khởi động",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void AppendSettingsWarning(string warning) =>
        SettingsWarning = string.IsNullOrWhiteSpace(SettingsWarning) ? warning : SettingsWarning + Environment.NewLine + warning;

    private void LoadSettings()
    {
        string path = Settings.DefaultUserPath;
        bool existed = File.Exists(path);
        AppSettings = Settings.Load(path, out string? warn);
        if (!string.IsNullOrWhiteSpace(warn)) AppendSettingsWarning(warn);
        string docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OjGen");
        // Mặc định (chưa có file cấu hình) hoặc đường dẫn tương đối: đặt trong Documents\OjGen để người dùng dễ tìm.
        if (!existed) AppSettings.OutputRoot = Path.Combine(docs, "output");
        else if (!Path.IsPathRooted(AppSettings.OutputRoot)) AppSettings.OutputRoot = Path.Combine(docs, AppSettings.OutputRoot);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { AutoThemeService.Stop(); } catch (Exception ex) { AppLog.Write("Automatic theme shutdown", ex); }
        base.OnExit(e);
    }
}
