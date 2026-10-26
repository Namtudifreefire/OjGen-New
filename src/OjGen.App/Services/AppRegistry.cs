using Microsoft.Win32;

namespace OjGen.Gui.Services;

/// <summary>Lưu vài thiết lập giao diện (theme, dạng bài gần nhất) vào HKCU\Software\OjGen, giống cách TQLTK dùng Registry.</summary>
internal static class AppRegistry
{
    private const string KeyPath = @"Software\OjGen";

    public static string GetString(string name, string defaultValue)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue(name)?.ToString() ?? defaultValue;
        }
        catch (Exception) { return defaultValue; }
    }

    public static void SetString(string name, string value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
            key?.SetValue(name, value);
        }
        catch (Exception) { }
    }

    public static bool GetBool(string name, bool defaultValue = false) =>
        bool.TryParse(GetString(name, defaultValue.ToString()), out var v) ? v : defaultValue;

    public static void SetBool(string name, bool value) => SetString(name, value.ToString());
}
