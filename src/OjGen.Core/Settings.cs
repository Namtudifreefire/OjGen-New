// Settings.cs - Cấu hình dùng chung (trình biên dịch, timeout, lời giải mặc định...).
// Lưu dạng key=value (UTF-8) trong ojgen_settings.ini.
//   - CLI: file nằm trong thư mục làm việc hiện tại (giống bản C++).
//   - Giao diện: %AppData%\OjGen\ojgen_settings.ini (xem Settings.DefaultUserPath).
using System.Globalization;
using System.Text;

namespace OjGen.Core;

public sealed class Settings
{
    public const string FileName = "ojgen_settings.ini";

    public string Compiler { get; set; } = "g++";
    /// <summary>Rỗng = dùng DefaultFlags.</summary>
    public string CxxFlags { get; set; } = "";
    public int TimeoutMs { get; set; } = 2000;
    public int CompileTimeoutSec { get; set; } = 120;
    public long MaxOutputMB { get; set; } = 64;
    /// <summary>0 = không giới hạn bộ nhớ.</summary>
    public long MemoryMB { get; set; } = 0;
    public bool NormalizeEol { get; set; } = true;
    public bool ZipCompress { get; set; } = true;
    public string Solution { get; set; } = "";
    public Dictionary<string, string> SolutionByProblem { get; set; } = new();
    public string OutputRoot { get; set; } = "output";
    public string Prefix { get; set; } = "test";
    /// <summary>Lệnh chạy Python cho lời giải .py; rỗng = tự tìm (python3/python/py).</summary>
    public string PythonCommand { get; set; } = "";

    /// <summary>Đường dẫn file đã nạp (dùng để ghi nhớ lời giải gần nhất); null = thư mục hiện tại.</summary>
    public string? SourcePath { get; set; }

    public string SolutionFor(string problemId) =>
        SolutionByProblem.TryGetValue(problemId, out var s) ? s : "";

    public static string DefaultFlags =>
        OperatingSystem.IsWindows()
            // -static: không cần libstdc++-6.dll khi chạy; --stack: tăng stack lên 256 MB
            // (mặc định của Windows chỉ 1-2 MB, dễ lỗi đệ quy sâu).
            ? "-O2 -std=c++17 -static -Wl,--stack=268435456"
            : "-O2 -std=c++17";

    public string EffectiveFlags => CxxFlags.Trim().Length == 0 ? DefaultFlags : CxxFlags;

    public static string DefaultUserPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OjGen", FileName);

    /// <summary>Đọc file cấu hình. Nếu file chưa có thì giữ mặc định.</summary>
    public static Settings Load(string? path, out string? warning)
    {
        warning = null;
        var s = new Settings { SourcePath = path };
        string file = path ?? FileName;
        if (!File.Exists(file)) return s;
        string data;
        try { data = File.ReadAllText(file, new UTF8Encoding(false)); }
        catch (Exception)
        {
            warning = $"Không đọc được {FileName}, dùng cấu hình mặc định.";
            return s;
        }
        if (data.Length > 0 && data[0] == '﻿') data = data[1..];
        var warn = new StringBuilder();
        int lineNo = 0;
        foreach (var rawLine in data.Split('\n'))
        {
            lineNo++;
            string t = rawLine.TrimEnd('\r').Trim();
            if (t.Length == 0 || t[0] == '#' || t[0] == ';' || t[0] == '[') continue;
            int eq = t.IndexOf('=');
            if (eq < 0) continue;
            string k = t[..eq].Trim(), v = t[(eq + 1)..].Trim();
            bool isNum = Util.ParseLL(v, out long num);
            if (k == "compiler") s.Compiler = v;
            else if (k == "cxxflags") s.CxxFlags = v;
            else if (k == "timeout_ms" && isNum && num >= 1 && num <= 3600000) s.TimeoutMs = (int)num;
            else if (k == "compile_timeout_sec" && isNum && num >= 1 && num <= 3600) s.CompileTimeoutSec = (int)num;
            else if (k == "max_output_mb" && isNum && num >= 1 && num <= 4096) s.MaxOutputMB = num;
            else if (k == "memory_mb" && isNum && num >= 0 && num <= 1048576) s.MemoryMB = num;
            else if (k == "normalize_eol" && isNum) s.NormalizeEol = num != 0;
            else if (k == "zip_compress" && isNum) s.ZipCompress = num != 0;
            else if (k == "clear_screen") { /* tùy chọn của menu C++ cũ: bỏ qua im lặng */ }
            else if (k == "solution") s.Solution = v;
            else if (k.StartsWith("solution.", StringComparison.Ordinal) && k.Length > 9) s.SolutionByProblem[k[9..]] = v;
            else if (k == "output_root" && v.Length > 0) s.OutputRoot = v;
            else if (k == "prefix" && v.Length > 0) s.Prefix = v;
            else if (k == "python") s.PythonCommand = v;
            else warn.Append($"  dòng {lineNo}: bỏ qua '{t}'\n");
        }
        if (warn.Length > 0) warning = $"Cảnh báo trong {FileName}:\n{warn}";
        return s;
    }

    public bool Save(out string? err)
    {
        err = null;
        var os = new StringBuilder();
        os.Append("# Cấu hình ojgen (sửa trực tiếp hoặc trong hộp thoại Cài đặt). Dòng bắt đầu bằng # là chú thích.\n");
        os.Append("compiler=").Append(Compiler).Append('\n');
        os.Append("# cxxflags để trống = dùng mặc định: ").Append(DefaultFlags).Append('\n');
        os.Append("cxxflags=").Append(CxxFlags).Append('\n');
        os.Append("timeout_ms=").Append(TimeoutMs.ToString(CultureInfo.InvariantCulture)).Append('\n');
        os.Append("compile_timeout_sec=").Append(CompileTimeoutSec.ToString(CultureInfo.InvariantCulture)).Append('\n');
        os.Append("max_output_mb=").Append(Util.LL(MaxOutputMB)).Append('\n');
        os.Append("memory_mb=").Append(Util.LL(MemoryMB)).Append('\n');
        os.Append("normalize_eol=").Append(NormalizeEol ? 1 : 0).Append('\n');
        os.Append("zip_compress=").Append(ZipCompress ? 1 : 0).Append('\n');
        os.Append("python=").Append(PythonCommand).Append('\n');
        os.Append("solution=").Append(Solution).Append('\n');
        foreach (var kv in SolutionByProblem.OrderBy(k => k.Key, StringComparer.Ordinal))
            os.Append("solution.").Append(kv.Key).Append('=').Append(kv.Value).Append('\n');
        os.Append("output_root=").Append(OutputRoot).Append('\n');
        os.Append("prefix=").Append(Prefix).Append('\n');
        try
        {
            string file = SourcePath ?? FileName;
            string? dir = Path.GetDirectoryName(Path.GetFullPath(file));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(file, os.ToString(), new UTF8Encoding(false));
            return true;
        }
        catch (Exception ex)
        {
            err = "Không ghi được file cấu hình: " + ex.Message;
            return false;
        }
    }

    public string Describe()
    {
        var os = new StringBuilder();
        os.Append("  Trình biên dịch       : ").Append(Compiler).Append('\n');
        os.Append("  Cờ biên dịch          : ").Append(EffectiveFlags).Append(CxxFlags.Trim().Length == 0 ? "  (mặc định)" : "").Append('\n');
        os.Append("  Timeout mỗi test      : ").Append(TimeoutMs).Append(" ms\n");
        os.Append("  Timeout biên dịch     : ").Append(CompileTimeoutSec).Append(" giây\n");
        os.Append("  Output tối đa mỗi test: ").Append(Util.LL(MaxOutputMB)).Append(" MB\n");
        os.Append("  Giới hạn bộ nhớ       : ").Append(MemoryMB > 0 ? Util.LL(MemoryMB) + " MB" : "không giới hạn").Append('\n');
        os.Append("  Chuẩn hóa CRLF -> LF  : ").Append(NormalizeEol ? "có" : "không").Append('\n');
        os.Append("  Nén ZIP (Deflate)     : ").Append(ZipCompress ? "có" : "không (chỉ lưu)").Append('\n');
        os.Append("  Lời giải mặc định     : ").Append(Solution.Length == 0 ? "(chưa đặt)" : Solution).Append('\n');
        os.Append("  Thư mục output        : ").Append(OutputRoot).Append('\n');
        os.Append("  Tiền tố tên test      : ").Append(Prefix).Append('\n');
        return os.ToString();
    }

    public Settings Clone()
    {
        var c = (Settings)MemberwiseClone();
        c.SolutionByProblem = new Dictionary<string, string>(SolutionByProblem);
        return c;
    }

    public string WorkDir => Path.Combine(OutputRoot, ".work");
}
