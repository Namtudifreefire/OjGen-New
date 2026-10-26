using System.Windows.Threading;

namespace OjGen.Gui.Services;

/// <summary>Tự động đổi theme Sáng/Tối theo giờ (6h–18h sáng, còn lại tối), kiểm tra mỗi phút.</summary>
internal static class AutoThemeService
{
    public static bool IsEnabled { get; private set; }
    public static int LightStartHour { get; set; } = 6;
    public static int DarkStartHour { get; set; } = 18;

    private static DispatcherTimer? _timer;

    public static void Start()
    {
        IsEnabled = true;
        ApplyByTime();
        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timer.Tick += (_, _) => ApplyByTime();
        _timer.Start();
    }

    public static void Stop()
    {
        IsEnabled = false;
        _timer?.Stop();
        _timer = null;
    }

    private static void ApplyByTime()
    {
        if (!IsEnabled) return;
        int hour = DateTime.Now.Hour;
        bool shouldBeLight = hour >= LightStartHour && hour < DarkStartHour;
        // Chỉ áp dụng khi theme thay đổi, tránh làm mới liên tục.
        if (shouldBeLight && ThemeService.IsDarkMode) ThemeService.ApplyLight(save: false);
        else if (!shouldBeLight && !ThemeService.IsDarkMode) ThemeService.ApplyDark(save: false);
    }
}
