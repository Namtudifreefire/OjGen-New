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
            try
            {
                MessageBox.Show($"Đã xảy ra lỗi:\n\n{args.Exception.Message}", "Lỗi hệ thống",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception) { }
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) => args.SetObserved();

        try { LoadSettings(); }
        catch (Exception ex) { SettingsWarning = "Không đọc được cấu hình: " + ex.Message; }
        try { ThemeService.Initialize(); } catch (Exception) { }
        try { if (AppRegistry.GetBool("AutoThemeEnabled")) AutoThemeService.Start(); } catch (Exception) { }

        try
        {
            var main = new MainWindow();
            MainWindow = main;
            main.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể khởi động ứng dụng:\n\n{ex}", "Lỗi khởi động",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void LoadSettings()
    {
        string path = Settings.DefaultUserPath;
        bool existed = File.Exists(path);
        AppSettings = Settings.Load(path, out string? warn);
        SettingsWarning = warn;
        string docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OjGen");
        // Mặc định (chưa có file cấu hình) hoặc đường dẫn tương đối: đặt trong Documents\OjGen để người dùng dễ tìm.
        if (!existed) AppSettings.OutputRoot = Path.Combine(docs, "output");
        else if (!Path.IsPathRooted(AppSettings.OutputRoot)) AppSettings.OutputRoot = Path.Combine(docs, AppSettings.OutputRoot);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { AutoThemeService.Stop(); } catch (Exception) { }
        base.OnExit(e);
    }
}
