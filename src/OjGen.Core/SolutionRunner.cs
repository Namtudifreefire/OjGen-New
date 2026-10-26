// SolutionRunner.cs - Biên dịch (nếu cần) và chạy lời giải chuẩn để tạo file output.
//
// Hỗ trợ: mã nguồn C++ (.cpp .cc .cxx .c++ .cp) -> biên dịch bằng trình biên dịch trong Settings,
//         Python (.py) -> chạy bằng python,
//         chương trình đã biên dịch (.exe trên Windows, file thực thi trên Linux/macOS).
using System.Text;

namespace OjGen.Core;

public sealed class PreparedSolution
{
    public bool Ok { get; set; }
    /// <summary>Lệnh chạy (phần tử đầu là chương trình).</summary>
    public List<string> Argv { get; set; } = new();
    public bool Compiled { get; set; }
    /// <summary>Dùng lại bản biên dịch trước (mã nguồn không đổi).</summary>
    public bool Cached { get; set; }
    /// <summary>Lỗi (kèm log biên dịch nếu có).</summary>
    public string Error { get; set; } = "";
}

public sealed record TestFailure(int Index, string Name, string Reason);

public sealed class OutputReport
{
    public int Total { get; set; }
    public int Ok { get; set; }
    public List<TestFailure> Failures { get; } = new();
    public List<string> Warnings { get; } = new();
    public long MaxMs { get; set; }
    public bool Cancelled { get; set; }
}

public static class SolutionRunner
{
    private static readonly string[] CppExts = { ".cpp", ".cc", ".cxx", ".c++", ".cp" };

    /// <summary>Tìm chương trình trong PATH (thêm .exe trên Windows). null nếu không thấy.</summary>
    public static string? FindOnPath(string name)
    {
        if (name.IndexOfAny(new[] { '/', '\\' }) >= 0) return File.Exists(name) ? name : null;
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;
        var exts = OperatingSystem.IsWindows() ? new[] { ".exe", ".cmd", ".bat", "" } : new[] { "" };
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var e in exts)
            {
                try
                {
                    string cand = Path.Combine(dir.Trim('"'), name + e);
                    if (File.Exists(cand)) return cand;
                }
                catch (Exception) { }
            }
        }
        return null;
    }

    /// <summary>Tìm trình biên dịch: nếu chỉ là tên mà không có trong PATH, trên Windows thử các thư mục MSYS2 mặc định.</summary>
    private static string ResolveCompiler(string name, IJobLog log)
    {
        if (name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0) return name;
        if (FindOnPath(name) != null) return name;
        if (OperatingSystem.IsWindows())
        {
            string[] dirs =
            {
                @"C:\msys64\ucrt64\bin", @"C:\msys64\mingw64\bin", @"C:\msys64\clang64\bin",
                @"D:\msys64\ucrt64\bin", @"D:\msys64\mingw64\bin",
            };
            string exe = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe";
            foreach (var d in dirs)
            {
                string cand = Path.Combine(d, exe);
                if (File.Exists(cand))
                {
                    log.Line($"  (Không thấy '{name}' trong PATH, dùng {cand})");
                    return cand;
                }
            }
        }
        return name;
    }

    private static List<string>? ResolvePython(Settings st)
    {
        if (st.PythonCommand.Trim().Length > 0)
        {
            var parts = Util.SplitArgs(st.PythonCommand);
            string? exe = FindOnPath(parts[0]);
            if (exe == null) return null;
            parts[0] = exe;
            return parts;
        }
        string[] names = OperatingSystem.IsWindows() ? new[] { "python", "py" } : new[] { "python3", "python" };
        foreach (var n in names)
        {
            string? exe = FindOnPath(n);
            if (exe == null) continue;
            var l = new List<string> { exe };
            if (n == "py") l.Add("-3");
            return l;
        }
        return null;
    }

    private static string FirstLines(string text, int maxLines)
    {
        var lines = text.Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        var sb = new StringBuilder();
        int n = 0;
        foreach (var raw in lines)
        {
            if (n == maxLines) { sb.Append("  ... (còn nữa)\n"); break; }
            sb.Append("  ").Append(raw.TrimEnd('\r')).Append('\n');
            n++;
        }
        return sb.ToString();
    }

    public static PreparedSolution Prepare(string path, Settings st, string workDir, bool forceRebuild,
                                           IJobLog log, CancellationToken ct = default)
    {
        var ps = new PreparedSolution();
        if (path.Trim().Length == 0) { ps.Error = "Chưa nhập đường dẫn file lời giải."; return ps; }
        if (!File.Exists(path))
        {
            ps.Error = Directory.Exists(path) ? "Đường dẫn không phải là file: " + path : "Không tìm thấy file lời giải: " + path;
            return ps;
        }
        string src = Path.GetFullPath(path);
        string ext = Path.GetExtension(src).ToLowerInvariant();

        if (ext == ".py")
        {
            var py = ResolvePython(st);
            if (py == null)
            {
                ps.Error = "Không tìm thấy Python để chạy lời giải .py. Hãy cài Python hoặc đặt đường dẫn trong Cài đặt (mục python).";
                return ps;
            }
            py.Add(src);
            ps.Ok = true;
            ps.Argv = py;
            log.Line("Chạy lời giải Python: " + src);
            return ps;
        }

        if (Array.IndexOf(CppExts, ext) < 0)
        {
            bool runnable;
            if (OperatingSystem.IsWindows()) runnable = ext == ".exe";
            else
            {
                runnable = ext != ".c" && ext != ".h";
                try
                {
                    runnable = runnable && (File.GetUnixFileMode(src) &
                        (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
                }
                catch (Exception) { runnable = false; }
            }
            if (!runnable)
            {
                ps.Error = "File lời giải phải là mã nguồn C++ (.cpp, .cc, .cxx), Python (.py) hoặc chương trình đã biên dịch" +
                           (OperatingSystem.IsWindows() ? " (.exe)" : "") + ". Nhận được: " + Path.GetFileName(src);
                return ps;
            }
            ps.Ok = true;
            ps.Argv = new List<string> { src };
            log.Line("Dùng trực tiếp chương trình đã biên dịch: " + src);
            return ps;
        }

        // ---- C++ ----
        if (!FileManager.ReadFile(src, out byte[] codeBytes, out string? rerr)) { ps.Error = rerr ?? "Không đọc được file."; return ps; }
        string code = Encoding.Latin1.GetString(codeBytes);
        if (code.Contains("freopen", StringComparison.Ordinal))
            log.Line("  ! Cảnh báo: lời giải có gọi freopen(). ojgen cấp input qua stdin và đọc output từ stdout.\n" +
                     "    Hãy bọc freopen trong  #ifndef OJGEN ... #endif  (ojgen tự định nghĩa macro OJGEN khi biên dịch).");
        try { Directory.CreateDirectory(workDir); }
        catch (Exception ex) { ps.Error = "Không tạo được thư mục làm việc: " + ex.Message; return ps; }

        string flags = st.EffectiveFlags;
        ulong h = Util.Fnv1a(codeBytes);
        h = Util.Fnv1aUtf8("\u001f" + st.Compiler + "\u001f" + flags + "\u001f" + src, h);
        string tag = Util.ToHex(h)[..12];
        string exeName = "sol_" + tag + (OperatingSystem.IsWindows() ? ".exe" : "");
        string exe = Path.GetFullPath(Path.Combine(workDir, exeName));

        if (!forceRebuild && File.Exists(exe))
        {
            ps.Ok = true;
            ps.Argv = new List<string> { exe };
            ps.Compiled = true;
            ps.Cached = true;
            log.Line("Mã nguồn không đổi so với lần biên dịch trước: dùng lại " + exeName);
            return ps;
        }

        // Biên dịch trong thư mục làm việc, dùng bản sao mã nguồn có tên ASCII và đường dẫn tương đối:
        // tránh lỗi với đường dẫn có dấu tiếng Việt ở các bản g++ MinGW cũ.
        string srcName = "src_" + tag + ext;
        if (!FileManager.WriteFile(Path.Combine(workDir, srcName), codeBytes, out string? werr)) { ps.Error = werr ?? ""; return ps; }
        string compiler = ResolveCompiler(st.Compiler, log);
        if (compiler.IndexOfAny(new[] { '/', '\\' }) >= 0 && File.Exists(compiler)) compiler = Path.GetFullPath(compiler);
        var args = new List<string> { compiler, srcName };
        args.AddRange(Util.SplitArgs(flags));
        args.Add("-DOJGEN");
        // Cho phép #include "file.h" nằm cạnh mã nguồn gốc.
        if (code.Contains("#include \"", StringComparison.Ordinal)) args.Add("-I" + (Path.GetDirectoryName(src) ?? "."));
        args.Add("-o");
        args.Add(exeName);

        var shown = new StringBuilder();
        for (int k = 0; k < args.Count; k++)
        {
            string a = k == 1 ? src : args[k];
            shown.Append(a.Contains(' ') ? "\"" + a + "\"" : a).Append(' ');
        }
        log.Line("Đang biên dịch: " + shown);

        string logPath = Path.Combine(workDir, "compile.log");
        var lim = new RunLimits { TimeoutMs = st.CompileTimeoutSec * 1000, MaxOutputBytes = 32UL << 20 };
        try { File.Delete(exe); } catch (Exception) { }
        RunResult rr = ProcessRunner.Run(args, null, logPath, logPath, Path.GetFullPath(workDir), lim, ct);
        try { File.Delete(Path.Combine(workDir, srcName)); } catch (Exception) { }
        string logText = FileManager.ReadText(logPath);
        // Đổi tên bản sao trong thông báo lỗi về lại tên file gốc của người dùng.
        logText = logText.Replace(srcName, src, StringComparison.Ordinal);
        if (rr.Status == RunStatus.Cancelled) { ps.Error = "Đã hủy trong lúc biên dịch."; return ps; }
        if (rr.Status != RunStatus.Ok || !File.Exists(exe))
        {
            ps.Error = "BIÊN DỊCH THẤT BẠI";
            if (rr.Status == RunStatus.StartFailed) ps.Error += ": " + rr.Message;
            else if (rr.Status == RunStatus.Timeout) ps.Error += $": quá {st.CompileTimeoutSec} giây.";
            if (logText.Length > 0) ps.Error += "\nThông báo của trình biên dịch:\n" + FirstLines(logText, 40);
            try { File.Delete(exe); } catch (Exception) { }
            return ps;
        }
        log.Line($"Biên dịch thành công ({rr.ElapsedMs} ms).");
        if (logText.Trim().Length > 0) log.Line("Trình biên dịch có cảnh báo:\n" + FirstLines(logText, 10));
        ps.Ok = true;
        ps.Argv = new List<string> { exe };
        ps.Compiled = true;
        return ps;
    }

    /// <summary>
    /// Chạy lời giải với từng file .inp, ghi .out tương ứng (cùng tên). File .out chỉ được tạo khi tiến
    /// trình kết thúc bình thường (mã thoát 0, không quá thời gian, không quá dung lượng).
    /// Test lỗi KHÔNG có file .out.
    /// </summary>
    public static OutputReport ProduceOutputs(IReadOnlyList<string> argv, IReadOnlyList<string> inputs, Settings st,
                                              string workDir, IJobLog log, CancellationToken ct = default)
    {
        var rep = new OutputReport { Total = inputs.Count };
        try { Directory.CreateDirectory(workDir); } catch (Exception) { }
        string tmpOut = Path.GetFullPath(Path.Combine(workDir, "run_stdout.tmp"));
        string tmpErr = Path.GetFullPath(Path.Combine(workDir, "run_stderr.tmp"));
        var lim = new RunLimits
        {
            TimeoutMs = st.TimeoutMs,
            MaxOutputBytes = (ulong)st.MaxOutputMB << 20,
            MemoryBytes = (ulong)st.MemoryMB << 20,
        };
        int width = Util.LL(inputs.Count).Length;
        var outHashes = new HashSet<ulong>();
        int emptyOutputs = 0;
        for (int i = 0; i < inputs.Count; i++)
        {
            if (ct.IsCancellationRequested) { rep.Cancelled = true; break; }
            string inp = inputs[i];
            string finalOut = Path.ChangeExtension(inp, ".out");
            try { File.Delete(finalOut); } catch (Exception) { }  // không để lại .out cũ không khớp với input
            string name = Path.GetFileNameWithoutExtension(inp);
            string idx = Util.LL(i + 1).PadLeft(width);
            log.Write($"  [{idx}/{inputs.Count}] {name} ... ");
            log.Progress("Chạy lời giải", i, inputs.Count);
            RunResult rr = ProcessRunner.Run(argv, Path.GetFullPath(inp), tmpOut, tmpErr, workDir, lim, ct, unixLimits: true);
            if (rr.Status == RunStatus.Cancelled) { log.Line("ĐÃ HỦY"); rep.Cancelled = true; break; }
            rep.MaxMs = Math.Max(rep.MaxMs, rr.ElapsedMs);
            string reason = "";
            if (rr.Status == RunStatus.Ok)
            {
                if (!FileManager.ReadFile(tmpOut, out byte[] data, out string? err)) reason = err ?? "";
                else
                {
                    if (st.NormalizeEol && Array.IndexOf(data, (byte)'\r') >= 0)
                    {
                        var t = new List<byte>(data.Length);
                        for (int k = 0; k < data.Length; k++)
                            if (!(data[k] == (byte)'\r' && k + 1 < data.Length && data[k + 1] == (byte)'\n')) t.Add(data[k]);
                        data = t.ToArray();
                    }
                    if (!FileManager.WriteFile(finalOut, data, out err)) reason = err ?? "";
                    else
                    {
                        rep.Ok++;
                        outHashes.Add(Util.Fnv1a(data));
                        if (data.Length == 0) emptyOutputs++;
                        log.Line($"OK ({rr.ElapsedMs} ms)");
                    }
                }
            }
            else
            {
                reason = ProcessRunner.StatusName(rr.Status) + ": " + rr.Message;
                string errText = FileManager.ReadText(tmpErr).Trim();
                if (errText.Length > 0)
                    reason += "\n      stderr: " + (errText.Length > 300 ? errText[..300] : errText).Trim();
            }
            if (reason.Length > 0)
            {
                rep.Failures.Add(new TestFailure(i + 1, name, reason));
                log.Line("LỖI\n      " + reason);
            }
        }
        log.Progress("Chạy lời giải", inputs.Count, inputs.Count);
        try { File.Delete(tmpOut); } catch (Exception) { }
        try { File.Delete(tmpErr); } catch (Exception) { }
        if (rep.Ok > 1 && outHashes.Count == 1 && inputs.Count > 1)
            rep.Warnings.Add($"Tất cả {rep.Ok} file output giống hệt nhau. Hãy kiểm tra lời giải có đọc input từ stdin không.");
        if (emptyOutputs > 0)
            rep.Warnings.Add($"{emptyOutputs} test có output rỗng (lời giải không in gì).");
        return rep;
    }
}
