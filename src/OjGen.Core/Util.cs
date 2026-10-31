// Util.cs - Hàm tiện ích dùng chung (chuyển từ util.cpp / common.h).
using System.Globalization;
using System.Text;

namespace OjGen.Core;

public static class Util
{
    public const string Version = "3.1.0";

    private static readonly UTF8Encoding Utf8NoBom = new(false);
    public static UTF8Encoding Utf8 => Utf8NoBom;

    /// <summary>Tách theo ký tự phân cách, giữ token rỗng (giống split() của bản C++).</summary>
    public static string[] Split(string s, char sep) => s.Split(sep);

    // ------------------------------------------------------------------ parse

    private static bool ParsePlain(string s, out long value)
    {
        value = 0;
        if (s.Length == 0) return false;
        int i = 0;
        if (s[0] == '+')
        {
            i = 1;
            if (i == s.Length || s[i] < '0' || s[i] > '9') return false;
        }
        return long.TryParse(s.AsSpan(i), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Phân tích số nguyên 64-bit chặt chẽ. Hỗ trợ "123", "-45", "+7", "1e9", "2e5"
    /// (ký hiệu khoa học với phần mũ nguyên) và dấu phân cách hàng nghìn "_" hoặc "'".
    /// Trả về false nếu sai định dạng hoặc vượt phạm vi long.
    /// </summary>
    public static bool ParseLL(string raw, out long value)
    {
        value = 0;
        string s = raw.Trim().Replace("_", "").Replace("'", "");
        if (s.Length == 0) return false;
        int epos = s.IndexOfAny(new[] { 'e', 'E' });
        if (epos < 0) return ParsePlain(s, out value);
        if (!ParsePlain(s[..epos], out long mant)) return false;
        if (!ParsePlain(s[(epos + 1)..], out long ex)) return false;
        if (ex < 0 || ex > 18) return false;
        Int128 v = mant;
        for (long i = 0; i < ex; i++)
        {
            v *= 10;
            if (v > long.MaxValue || v < long.MinValue) return false;
        }
        value = (long)v;
        return true;
    }

    public static bool ParseULL(string raw, out ulong value)
    {
        value = 0;
        string s = raw.Trim();
        if (s.Length == 0 || s[0] == '-') return false;
        int i = 0;
        if (s[0] == '+') i = 1;
        if (i == s.Length || s[i] < '0' || s[i] > '9') return false;
        return ulong.TryParse(s.AsSpan(i), NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Token số nguyên đúng định dạng xuất (chỉ chữ số, dấu '-', không có số 0 thừa ở đầu).</summary>
    public static bool ParseStrictToken(string s, out long value)
    {
        value = 0;
        if (s.Length == 0) return false;
        int i = 0;
        if (s[0] == '-')
        {
            if (s.Length == 1) return false;
            i = 1;
        }
        for (int j = i; j < s.Length; j++)
            if (s[j] < '0' || s[j] > '9') return false;
        if (s.Length - i > 1 && s[i] == '0') return false;
        if (s == "-0") return false;
        return long.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    // ---------------------------------------------------------------- số học

    /// <summary>Độ rộng đoạn [lo, hi] dưới dạng unsigned (hi - lo), an toàn với tràn số.</summary>
    public static ulong SpanOf(long lo, long hi) => unchecked((ulong)hi - (ulong)lo);

    /// <summary>Số ký tự tối đa của một giá trị trong [lo, hi] (kể cả dấu '-').</summary>
    public static ulong MaxDigits(long lo, long hi)
    {
        ulong a = (ulong)lo.ToString(CultureInfo.InvariantCulture).Length;
        ulong b = (ulong)hi.ToString(CultureInfo.InvariantCulture).Length;
        return a > b ? a : b;
    }

    /// <summary>Nối số nguyên vào chuỗi, luôn dùng dấu '-' ASCII bất kể ngôn ngữ của hệ điều hành.</summary>
    public static void AppendLL(StringBuilder sb, long v)
    {
        Span<char> buf = stackalloc char[24];
        v.TryFormat(buf, out int n, default, CultureInfo.InvariantCulture);
        sb.Append(buf[..n]);
    }

    public static string LL(long v) => v.ToString(CultureInfo.InvariantCulture);
    public static string LL(ulong v) => v.ToString(CultureInfo.InvariantCulture);

    public static string FmtBytes(ulong bytes)
    {
        var ci = CultureInfo.InvariantCulture;
        if (bytes < 1024UL) return bytes.ToString(ci) + " B";
        if (bytes < 1024UL * 1024) return (bytes / 1024.0).ToString("F1", ci) + " KB";
        if (bytes < 1024UL * 1024 * 1024) return (bytes / 1048576.0).ToString("F1", ci) + " MB";
        return (bytes / 1073741824.0).ToString("F2", ci) + " GB";
    }

    // ------------------------------------------------------------------ băm

    public const ulong FnvOffset = 1469598103934665603UL;

    /// <summary>Hàm băm FNV-1a 64-bit.</summary>
    public static ulong Fnv1a(ReadOnlySpan<byte> data, ulong h = FnvOffset)
    {
        foreach (byte c in data)
        {
            h ^= c;
            unchecked { h *= 1099511628211UL; }
        }
        return h;
    }

    /// <summary>FNV-1a trên chuỗi chỉ gồm ASCII (nội dung file input): mỗi ký tự là một byte.</summary>
    public static ulong Fnv1aAscii(string data, ulong h = FnvOffset)
    {
        foreach (char ch in data)
        {
            h ^= (byte)ch;
            unchecked { h *= 1099511628211UL; }
        }
        return h;
    }

    public static ulong Fnv1aUtf8(string data, ulong h = FnvOffset) => Fnv1a(Utf8NoBom.GetBytes(data), h);

    public static string ToHex(ulong v) => v.ToString("x16", CultureInfo.InvariantCulture);

    // ----------------------------------------------------------- tách đối số

    /// <summary>Tách chuỗi cờ biên dịch thành danh sách đối số, hỗ trợ "..." cho giá trị có dấu cách.</summary>
    public static List<string> SplitArgs(string s)
    {
        var res = new List<string>();
        var cur = new StringBuilder();
        bool inQ = false, has = false;
        foreach (char c in s)
        {
            if (c == '"') { inQ = !inQ; has = true; }
            else if (!inQ && (c == ' ' || c == '\t'))
            {
                if (has) { res.Add(cur.ToString()); cur.Clear(); has = false; }
            }
            else { cur.Append(c); has = true; }
        }
        if (has) res.Add(cur.ToString());
        return res;
    }
}
