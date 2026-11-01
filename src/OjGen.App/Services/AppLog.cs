using System.IO;
using System.Text;

namespace OjGen.Gui.Services;

internal static class AppLog
{
    private const long MaxLogBytes = 2 * 1024 * 1024;
    private static readonly object Sync = new();

    public static string FilePath
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root)) root = Path.GetTempPath();
            return Path.Combine(root, "OjGen", "logs", "ojgen.log");
        }
    }

    public static void Write(string context, Exception? exception = null)
    {
        try
        {
            string detail = exception?.ToString() ?? "(không có chi tiết exception)";
            if (detail.Length > 64 * 1024) detail = detail[..(64 * 1024)] + "\n... (đã rút gọn)";
            string record = $"[{DateTimeOffset.Now:O}] {context}\n{detail}\n\n";
            lock (Sync)
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (File.Exists(path) && new FileInfo(path).Length + Encoding.UTF8.GetByteCount(record) > MaxLogBytes)
                    File.Move(path, path + ".1", overwrite: true);
                File.AppendAllText(path, record, new UTF8Encoding(false));
            }
        }
        catch (Exception) { /* Diagnostics must never prevent application startup or shutdown. */ }
    }
}
