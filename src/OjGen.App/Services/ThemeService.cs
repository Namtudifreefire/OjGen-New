using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OjGen.Gui.Services;

/// <summary>
/// Quản lý theme Sáng/Tối (cách làm của TQLTK): hoán đổi ResourceDictionary LightTheme/DarkTheme ở đầu danh sách,
/// các control dùng DynamicResource nên tự đổi màu; lưu lựa chọn vào Registry.
/// </summary>
internal static class ThemeService
{
    private const string LightThemePath = "Themes/LightTheme.xaml";
    private const string DarkThemePath = "Themes/DarkTheme.xaml";

    public static bool IsDarkMode { get; private set; }

    public static void Initialize()
    {
        if (string.Equals(AppRegistry.GetString("Theme", "Light"), "Dark", StringComparison.OrdinalIgnoreCase))
            ApplyDark(save: false);
        else
            ApplyLight(save: false);
    }

    public static void Toggle()
    {
        if (IsDarkMode) ApplyLight();
        else ApplyDark();
    }

    public static void ApplyLight(bool save = true)
    {
        SwapTheme(LightThemePath);
        IsDarkMode = false;
        if (save) AppRegistry.SetString("Theme", "Light");
        RefreshAllWindows();
    }

    public static void ApplyDark(bool save = true)
    {
        SwapTheme(DarkThemePath);
        IsDarkMode = true;
        if (save) AppRegistry.SetString("Theme", "Dark");
        RefreshAllWindows();
    }

    private static void SwapTheme(string newThemePath)
    {
        var app = Application.Current;
        if (app == null) return;
        var dicts = app.Resources.MergedDictionaries;
        var newDict = new ResourceDictionary { Source = new Uri(newThemePath, UriKind.Relative) };
        var old = dicts.FirstOrDefault(d => d.Source != null &&
            (d.Source.OriginalString.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
             d.Source.OriginalString.EndsWith("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase)));
        if (old != null) dicts.Remove(old);
        dicts.Insert(0, newDict);
    }

    private static void RefreshAllWindows()
    {
        try
        {
            foreach (Window w in Application.Current.Windows) RefreshTree(w);
        }
        catch (Exception) { }
    }

    private static void RefreshTree(DependencyObject root)
    {
        try
        {
            if (root is FrameworkElement fe) { fe.InvalidateVisual(); fe.UpdateLayout(); }
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is Control ctrl)
                {
                    ctrl.InvalidateProperty(Control.BackgroundProperty);
                    ctrl.InvalidateProperty(Control.ForegroundProperty);
                }
                RefreshTree(child);
            }
        }
        catch (Exception) { }
    }
}
