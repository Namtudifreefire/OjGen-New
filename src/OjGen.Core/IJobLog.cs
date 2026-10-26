// IJobLog.cs - Kênh thông báo của phần lõi (CLI in ra console, giao diện WPF ghi vào ô nhật ký).
namespace OjGen.Core;

public interface IJobLog
{
    /// <summary>Ghi văn bản thô (có thể không kết thúc bằng xuống dòng).</summary>
    void Write(string text);

    /// <summary>Báo tiến độ (giai đoạn, đã xong, tổng). Giao diện dùng để cập nhật thanh tiến độ.</summary>
    void Progress(string phase, int done, int total);
}

public sealed class NullLog : IJobLog
{
    public static readonly NullLog Instance = new();
    public void Write(string text) { }
    public void Progress(string phase, int done, int total) { }
}

/// <summary>Ghi nhật ký vào bộ nhớ (dùng cho kiểm thử).</summary>
public sealed class StringLog : IJobLog
{
    private readonly System.Text.StringBuilder _sb = new();
    public void Write(string text) { lock (_sb) _sb.Append(text); }
    public void Progress(string phase, int done, int total) { }
    public override string ToString() { lock (_sb) return _sb.ToString(); }
}

public static class LogExt
{
    public static void Line(this IJobLog log, string text = "") => log.Write(text + "\n");
}
