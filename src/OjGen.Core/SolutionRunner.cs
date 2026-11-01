// SolutionRunner.cs - Biên dịch (nếu cần) và chạy lời giải chuẩn để tạo file output.
//
// Hỗ trợ C/C++, Python, Java source, Kotlin, Pascal, Rust, Go, C# single-file, run profile nhiều file,
// và chương trình đã biên dịch.
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace OjGen.Core;

public sealed class PreparedSolution
{
    public bool Ok { get; set; }
    /// <summary>Lệnh chạy (phần tử đầu là chương trình).</summary>
    public List<string> Argv { get; set; } = new();
    public bool Compiled { get; set; }
    /// <summary>Dùng lại bản biên dịch trước (mã nguồn không đổi).</summary>
    public bool Cached { get; set; }
    /// <summary>Temporary output created only for the single-file C# runner.</summary>
    public string TemporaryDirectory { get; set; } = "";
    /// <summary>Optional working directory used when running each test.</summary>
    public string WorkingDirectory { get; set; } = "";
    /// <summary>Lỗi (kèm log biên dịch nếu có).</summary>
    public string Error { get; set; } = "";
}

public sealed record TestFailure(int Index, string Name, string Reason);
public sealed record TestRunMetric(int Index, string Name, long ElapsedMs, ulong PeakMemoryBytes, RunStatus Status);

public sealed class OutputReport
{
    public int Total { get; set; }
    public int Ok { get; set; }
    public List<TestFailure> Failures { get; } = new();
    public List<string> Warnings { get; } = new();
    public long MaxMs { get; set; }
    public ulong PeakMemoryBytes { get; set; }
    public List<TestRunMetric> Metrics { get; } = new();
    public bool Cancelled { get; set; }
    public BenchmarkSummary Benchmark => BenchmarkSummary.From(Metrics);
}

public sealed record BenchmarkSummary(int Count, double MedianMs, double P95Ms, long PeakMemoryBytes)
{
    public static BenchmarkSummary From(IEnumerable<TestRunMetric> source)
    {
        var values = source.OrderBy(item => item.ElapsedMs).ToList();
        if (values.Count == 0) return new BenchmarkSummary(0, 0, 0, 0);
        double At(double p)
        {
            double index = Math.Clamp((values.Count - 1) * p, 0, values.Count - 1);
            int low = (int)Math.Floor(index), high = (int)Math.Ceiling(index);
            return values[low].ElapsedMs + (values[high].ElapsedMs - values[low].ElapsedMs) * (index - low);
        }
        ulong peak = values.Max(item => item.PeakMemoryBytes);
        return new BenchmarkSummary(values.Count, At(.5), At(.95), peak > long.MaxValue ? long.MaxValue : (long)peak);
    }
}

public static class SolutionRunner
{
    private static readonly string[] CppExts = { ".cpp", ".cc", ".cxx", ".c++", ".cp" };
    private const string OutputCheckpointFile = ".ojgen_output_checkpoint.json";

    private sealed class OutputCheckpoint
    {
        public int Version { get; set; } = 1;
        public string Fingerprint { get; set; } = "";
        public Dictionary<string, OutputCheckpointEntry> Outputs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class OutputCheckpointEntry
    {
        public string InputSha256 { get; set; } = "";
        public string OutputSha256 { get; set; } = "";
    }

    /// <summary>Stable identity for resuming output execution with the same solution and runtime limits.</summary>
    public static string CreateResumeFingerprint(string solutionPath, Settings settings)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string value) => sha.AppendData(Encoding.UTF8.GetBytes(value + "\0"));
        string fullPath = Path.GetFullPath(solutionPath);
        Add(fullPath);
        Add(Path.GetExtension(fullPath).ToLowerInvariant());
        Add(settings.Compiler); Add(settings.EffectiveFlags); Add(settings.PythonCommand);
        Add(settings.CompileTimeoutSec.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add(settings.TimeoutMs.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add(settings.MaxOutputMB.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add(settings.MemoryMB.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add(settings.NormalizeEol.ToString()); Add(settings.Prefix);
        using var source = File.OpenRead(fullPath);
        sha.AppendData(SHA256.HashData(source));
        if (string.Equals(Path.GetExtension(fullPath), ".ojrun.json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var profile = SolutionRunProfile.Load(fullPath);
                string sourceDir = Path.GetDirectoryName(fullPath)!;
                foreach (string file in profile.BuildArguments.Concat(profile.RunArguments)
                    .Select(value => value.Replace("{sourceDir}", sourceDir, StringComparison.OrdinalIgnoreCase))
                    .Where(value => File.Exists(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                {
                    Add(file);
                    using var dependency = File.OpenRead(file);
                    sha.AppendData(SHA256.HashData(dependency));
                }
            }
            catch (Exception) { }
        }
        return Convert.ToHexString(sha.GetHashAndReset());
    }

    private static string FileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool SaveOutputCheckpoint(string path, OutputCheckpoint checkpoint, out string? error)
    {
        error = null;
        try
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(checkpoint), new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) { error = "Không lưu được checkpoint output: " + ex.Message; return false; }
    }

    public static void Cleanup(PreparedSolution solution)
    {
        string path = solution.TemporaryDirectory;
        solution.TemporaryDirectory = "";
        if (path.Length == 0) return;
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch (Exception) { }
    }

    public static void CompleteOutputCheckpoint(string outDir)
    {
        try { File.Delete(Path.Combine(outDir, OutputCheckpointFile)); } catch (Exception) { }
    }

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

        if (ext == ".json" && src.EndsWith(".ojrun.json", StringComparison.OrdinalIgnoreCase))
            return PrepareProfile(src, st, workDir, log, ct);

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

        if (ext == ".java")
        {
            string? java = FindOnPath("java");
            if (java == null) { ps.Error = "Không tìm thấy Java runtime (java) trong PATH."; return ps; }
            ps.Ok = true;
            ps.Argv = new List<string> { java, src };
            log.Line("Chạy lời giải Java source launcher: " + src);
            return ps;
        }

        if (ext == ".cs")
        {
            string? dotnet = FindOnPath("dotnet");
            if (dotnet == null) { ps.Error = "Không tìm thấy .NET SDK (dotnet) trong PATH để biên dịch lời giải C# đơn file."; return ps; }
            string buildDir = Path.GetFullPath(Path.Combine(workDir, "ojgen-csharp-" + Guid.NewGuid().ToString("N")));
            try
            {
                Directory.CreateDirectory(buildDir);
                string sourceName = "Program.cs";
                File.Copy(src, Path.Combine(buildDir, sourceName));
                File.WriteAllText(Path.Combine(buildDir, "Solution.csproj"),
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>disable</Nullable></PropertyGroup></Project>",
                    new UTF8Encoding(false));
                string buildLog = Path.Combine(buildDir, "build.log");
                var build = ProcessRunner.Run(new[] { dotnet, "build", "Solution.csproj", "-c", "Release", "--nologo", "-o", "publish" },
                    null, buildLog, buildLog, buildDir, new RunLimits { TimeoutMs = st.CompileTimeoutSec * 1000, MaxOutputBytes = 32UL << 20 }, ct);
                if (build.Status != RunStatus.Ok || !File.Exists(Path.Combine(buildDir, "publish", "Solution.dll")))
                {
                    ps.Error = "BIÊN DỊCH C# THẤT BẠI" + (build.Status == RunStatus.Timeout ? $": quá {st.CompileTimeoutSec} giây." : "") +
                        (File.Exists(buildLog) ? "\n" + FirstLines(File.ReadAllText(buildLog), 30) : "\n" + build.Message);
                    try { Directory.Delete(buildDir, true); } catch (Exception) { }
                    return ps;
                }
                ps.Ok = true;
                ps.Compiled = true;
                ps.Argv = new List<string> { dotnet, Path.Combine(buildDir, "publish", "Solution.dll") };
                ps.TemporaryDirectory = buildDir;
                log.Line($"Biên dịch C# thành công ({build.ElapsedMs} ms).");
                return ps;
            }
            catch (Exception ex)
            {
                try { if (Directory.Exists(buildDir)) Directory.Delete(buildDir, recursive: true); } catch (Exception) { }
                ps.Error = "Không chuẩn bị được lời giải C#: " + ex.Message;
                return ps;
            }
        }

        if (ext == ".kt")
            return PrepareKotlin(src, st, workDir, log, ct);

        if (ext is ".rs" or ".go" or ".c" or ".pas" or ".pp")
            return PrepareAdditionalLanguage(src, ext, st, workDir, log, ct);

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
            ps.Error = "File lời giải phải là C/C++ (.c/.cpp), Python (.py), Java (.java), Kotlin (.kt), Pascal (.pas/.pp), C# (.cs), Rust (.rs), Go (.go), run profile (.ojrun.json) hoặc chương trình đã biên dịch" +
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

    private static PreparedSolution PrepareProfile(string path, Settings st, string workDir, IJobLog log, CancellationToken ct)
    {
        var result = new PreparedSolution();
        string profileDir = Path.GetDirectoryName(path)!;
        string buildDir = Path.Combine(Path.GetFullPath(workDir), "ojgen-profile-" + Guid.NewGuid().ToString("N"));
        string exePath = Path.Combine(buildDir, "solution" + (OperatingSystem.IsWindows() ? ".exe" : ""));
        try
        {
            var profile = SolutionRunProfile.Load(path);
            Directory.CreateDirectory(buildDir);
            string runExe = ResolveProfileToken(profile.RunExecutable, profileDir, buildDir, exePath);
            string runCwd = profile.WorkingDirectory.Length == 0 ? profileDir : ResolveProfileToken(profile.WorkingDirectory, profileDir, buildDir, exePath);
            if (!Path.IsPathRooted(runCwd)) runCwd = Path.GetFullPath(Path.Combine(profileDir, runCwd));
            if (!Directory.Exists(runCwd)) throw new DirectoryNotFoundException("WorkingDirectory không tồn tại: " + runCwd);
            if (profile.BuildExecutable.Length > 0)
            {
                string buildExe = ResolveProfileExecutable(profile.BuildExecutable, profileDir, buildDir);
                var buildArgs = profile.BuildArguments.Select(arg => ResolveProfileToken(arg, profileDir, buildDir, exePath)).ToList();
                string buildLog = Path.Combine(buildDir, "build.log");
                RunResult build = ProcessRunner.Run(new[] { buildExe }.Concat(buildArgs).ToList(), null, buildLog, buildLog,
                    profileDir, new RunLimits { TimeoutMs = st.CompileTimeoutSec * 1000, MaxOutputBytes = 32UL << 20 }, ct);
                if (build.Status != RunStatus.Ok)
                {
                    result.Error = "Build profile thất bại: " + ProcessRunner.StatusName(build.Status) +
                        (File.Exists(buildLog) ? "\n" + FirstLines(File.ReadAllText(buildLog), 30) : "\n" + build.Message);
                    try { Directory.Delete(buildDir, true); } catch { }
                    return result;
                }
                log.Line($"Build profile thành công ({build.ElapsedMs} ms).");
            }
            string runPath = ResolveProfileExecutable(runExe, profileDir, buildDir);
            if (runPath.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 && !File.Exists(runPath))
                throw new FileNotFoundException("Không tìm thấy chương trình chạy profile.", runPath);
            result.Ok = true;
            result.Compiled = profile.BuildExecutable.Length > 0;
            result.TemporaryDirectory = buildDir;
            result.WorkingDirectory = runCwd;
            result.Argv = new[] { runPath }.Concat(profile.RunArguments.Select(arg => ResolveProfileToken(arg, profileDir, buildDir, exePath))).ToList();
            log.Line("Đã nạp run profile nhiều file: " + path);
            return result;
        }
        catch (Exception ex)
        {
            try { if (Directory.Exists(buildDir)) Directory.Delete(buildDir, true); } catch { }
            result.Error = "Không nạp được run profile: " + ex.Message;
            return result;
        }
    }

    private static string ResolveProfileToken(string value, string sourceDir, string workDir, string exePath) => value
        .Replace("{sourceDir}", sourceDir, StringComparison.OrdinalIgnoreCase)
        .Replace("{workDir}", workDir, StringComparison.OrdinalIgnoreCase)
        .Replace("{exe}", exePath, StringComparison.OrdinalIgnoreCase);

    private static string ResolveProfileExecutable(string value, string sourceDir, string workDir)
    {
        string resolved = ResolveProfileToken(value, sourceDir, workDir, Path.Combine(workDir, "solution" + (OperatingSystem.IsWindows() ? ".exe" : "")));
        if (Path.IsPathRooted(resolved)) return Path.GetFullPath(resolved);
        if (resolved.IndexOfAny(new[] { '/', '\\' }) >= 0) return Path.GetFullPath(Path.Combine(sourceDir, resolved));
        return FindOnPath(resolved) ?? resolved;
    }

    private static PreparedSolution PrepareAdditionalLanguage(string src, string ext, Settings st, string workDir, IJobLog log, CancellationToken ct)
    {
        var result = new PreparedSolution();
        string? compiler = ext switch
        {
            ".rs" => FindOnPath("rustc"),
            ".go" => FindOnPath("go"),
            ".pas" or ".pp" => FindOnPath("fpc"),
            _ => FindOnPath("gcc"),
        };
        string language = ext switch { ".rs" => "Rust", ".go" => "Go", ".pas" or ".pp" => "Pascal", _ => "C" };
        string compilerName = ext switch { ".rs" => "rustc", ".go" => "Go (go)", ".pas" or ".pp" => "Free Pascal (fpc)", _ => "gcc" };
        if (compiler == null) { result.Error = $"Không tìm thấy {compilerName} trong PATH để chạy lời giải {language}."; return result; }
        string buildDir = Path.Combine(Path.GetFullPath(workDir), "ojgen-" + language.ToLowerInvariant() + "-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(buildDir);
            string exeName = "solution" + (OperatingSystem.IsWindows() ? ".exe" : "");
            string exe = Path.Combine(buildDir, exeName);
            List<string> args = ext switch
            {
                ".rs" => new List<string> { src, "--edition=2021", "-O", "-o", exe },
                ".go" => new List<string> { "build", "-o", exe, src },
                ".pas" or ".pp" => new List<string> { "-O2", "-o" + exe, src },
                _ => new List<string> { src }.Concat(Util.SplitArgs(st.CxxFlags.Trim().Length == 0 ?
                    (OperatingSystem.IsWindows() ? "-O2 -std=c17 -static -Wl,--stack=268435456" : "-O2 -std=c17") : st.CxxFlags)).Concat(new[] { "-o", exe }).ToList(),
            };
            string compileLog = Path.Combine(buildDir, "compile.log");
            RunResult run = ProcessRunner.Run(new[] { compiler }.Concat(args).ToList(), null, compileLog, compileLog,
                Path.GetDirectoryName(src)!, new RunLimits { TimeoutMs = st.CompileTimeoutSec * 1000, MaxOutputBytes = 32UL << 20 }, ct);
            if (run.Status != RunStatus.Ok || !File.Exists(exe))
            {
                result.Error = "Biên dịch thất bại: " + ProcessRunner.StatusName(run.Status) +
                    (File.Exists(compileLog) ? "\n" + FirstLines(File.ReadAllText(compileLog), 30) : "\n" + run.Message);
                try { Directory.Delete(buildDir, true); } catch { }
                return result;
            }
            result.Ok = true; result.Compiled = true; result.TemporaryDirectory = buildDir; result.Argv = new() { exe };
            log.Line($"Biên dịch {language} thành công ({run.ElapsedMs} ms).");
            return result;
        }
        catch (Exception ex)
        {
            try { if (Directory.Exists(buildDir)) Directory.Delete(buildDir, true); } catch { }
            result.Error = "Không chuẩn bị được lời giải: " + ex.Message;
            return result;
        }
    }

    private static PreparedSolution PrepareKotlin(string src, Settings st, string workDir, IJobLog log, CancellationToken ct)
    {
        var result = new PreparedSolution();
        string? kotlinCompiler = FindOnPath("kotlinc");
        if (kotlinCompiler == null)
        {
            result.Error = "Không tìm thấy Kotlin compiler (kotlinc) trong PATH.";
            return result;
        }
        string? java = FindOnPath("java");
        if (java == null)
        {
            result.Error = "Không tìm thấy Java runtime (java) trong PATH để chạy Kotlin.";
            return result;
        }
        string buildDir = Path.Combine(Path.GetFullPath(workDir), "ojgen-kotlin-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(buildDir);
            string jar = Path.Combine(buildDir, "solution.jar");
            List<string> compilerArgv;
            string kotlinExt = Path.GetExtension(kotlinCompiler);
            if (OperatingSystem.IsWindows() && (kotlinExt.Equals(".bat", StringComparison.OrdinalIgnoreCase) || kotlinExt.Equals(".cmd", StringComparison.OrdinalIgnoreCase)))
            {
                // Windows installs kotlinc as a batch wrapper. Call its compiler jars directly so
                // ProcessRunner can keep using ProcessStartInfo without invoking cmd.exe.
                string binDir = Path.GetDirectoryName(kotlinCompiler)!;
                string libDir = Path.GetFullPath(Path.Combine(binDir, "..", "lib"));
                if (!Directory.Exists(libDir) || !File.Exists(Path.Combine(libDir, "kotlin-compiler.jar")))
                    throw new InvalidDataException("Không tìm thấy thư viện Kotlin compiler cạnh kotlinc.bat.");
                compilerArgv = new List<string> { java, "-cp", Path.Combine(libDir, "*"), "org.jetbrains.kotlin.cli.jvm.K2JVMCompiler", src, "-include-runtime", "-d", jar };
            }
            else
                compilerArgv = new List<string> { kotlinCompiler, src, "-include-runtime", "-d", jar };

            string compileLog = Path.Combine(buildDir, "compile.log");
            RunResult run = ProcessRunner.Run(compilerArgv, null, compileLog, compileLog,
                Path.GetDirectoryName(src)!, new RunLimits { TimeoutMs = st.CompileTimeoutSec * 1000, MaxOutputBytes = 32UL << 20 }, ct);
            if (run.Status != RunStatus.Ok || !File.Exists(jar))
            {
                result.Error = "Biên dịch Kotlin thất bại: " + ProcessRunner.StatusName(run.Status) +
                    (File.Exists(compileLog) ? "\n" + FirstLines(File.ReadAllText(compileLog), 30) : "\n" + run.Message);
                try { Directory.Delete(buildDir, true); } catch { }
                return result;
            }
            result.Ok = true;
            result.Compiled = true;
            result.TemporaryDirectory = buildDir;
            result.Argv = new List<string> { java, "-jar", jar };
            log.Line($"Biên dịch Kotlin thành công ({run.ElapsedMs} ms).");
            return result;
        }
        catch (Exception ex)
        {
            try { if (Directory.Exists(buildDir)) Directory.Delete(buildDir, true); } catch { }
            result.Error = "Không chuẩn bị được lời giải Kotlin: " + ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Chạy lời giải với từng file .inp, ghi .out tương ứng (cùng tên). File .out chỉ được tạo khi tiến
    /// trình kết thúc bình thường (mã thoát 0, không quá thời gian, không quá dung lượng).
    /// Test lỗi KHÔNG có file .out.
    /// </summary>
    public static OutputReport ProduceOutputs(IReadOnlyList<string> argv, IReadOnlyList<string> inputs, Settings st,
                                              string workDir, IJobLog log, CancellationToken ct = default,
                                              bool resume = false, string? resumeFingerprint = null, string? solutionWorkingDirectory = null)
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
        string checkpointPath = "";
        OutputCheckpoint? checkpoint = null;
        if (resume)
        {
            if (inputs.Count == 0) return rep;
            checkpointPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(inputs[0]))!, OutputCheckpointFile);
            if (string.IsNullOrWhiteSpace(resumeFingerprint))
            {
                rep.Failures.Add(new TestFailure(0, "checkpoint", "Thiếu dấu vân tay lời giải để tiếp tục an toàn."));
                return rep;
            }
            try
            {
                if (File.Exists(checkpointPath))
                {
                    checkpoint = JsonSerializer.Deserialize<OutputCheckpoint>(File.ReadAllText(checkpointPath));
                    if (checkpoint == null || checkpoint.Version != 1 || !string.Equals(checkpoint.Fingerprint, resumeFingerprint, StringComparison.OrdinalIgnoreCase))
                    {
                        log.Line("Checkpoint output thuộc lời giải/cấu hình khác hoặc không đúng phiên bản; sẽ chạy lại toàn bộ output.");
                        checkpoint = new OutputCheckpoint { Fingerprint = resumeFingerprint };
                    }
                }
                checkpoint ??= new OutputCheckpoint { Fingerprint = resumeFingerprint };
            }
            catch (Exception ex)
            {
                log.Line("Checkpoint output hỏng hoặc không đọc được; sẽ chạy lại toàn bộ output: " + ex.Message);
                checkpoint = new OutputCheckpoint { Fingerprint = resumeFingerprint };
            }
        }
        for (int i = 0; i < inputs.Count; i++)
        {
            if (ct.IsCancellationRequested) { rep.Cancelled = true; break; }
            string inp = inputs[i];
            string finalOut = Path.ChangeExtension(inp, ".out");
            string name = Path.GetFileNameWithoutExtension(inp);
            string idx = Util.LL(i + 1).PadLeft(width);
            string inputHash = resume ? FileSha256(inp) : "";
            if (resume && checkpoint!.Outputs.TryGetValue(Path.GetFileName(finalOut), out var previous) &&
                string.Equals(previous.InputSha256, inputHash, StringComparison.OrdinalIgnoreCase) && File.Exists(finalOut) &&
                string.Equals(previous.OutputSha256, FileSha256(finalOut), StringComparison.OrdinalIgnoreCase))
            {
                rep.Ok++;
                try
                {
                    byte[] existing = File.ReadAllBytes(finalOut);
                    outHashes.Add(Util.Fnv1a(existing));
                    if (existing.Length == 0) emptyOutputs++;
                }
                catch (Exception) { }
                log.Write($"  [{idx}/{inputs.Count}] {name} ... ");
                log.Line("đã có trong checkpoint");
                log.Progress("Tiếp tục output", i + 1, inputs.Count);
                continue;
            }
            try { if (File.Exists(finalOut)) File.Delete(finalOut); } catch (Exception) { }
            if (resume) checkpoint!.Outputs.Remove(Path.GetFileName(finalOut));
            log.Write($"  [{idx}/{inputs.Count}] {name} ... ");
            log.Progress("Chạy lời giải", i, inputs.Count);
            RunResult rr = ProcessRunner.Run(argv, Path.GetFullPath(inp), tmpOut, tmpErr,
                string.IsNullOrWhiteSpace(solutionWorkingDirectory) ? workDir : solutionWorkingDirectory, lim, ct, unixLimits: true);
            if (rr.Status == RunStatus.Cancelled) { log.Line("ĐÃ HỦY"); rep.Cancelled = true; break; }
            rep.MaxMs = Math.Max(rep.MaxMs, rr.ElapsedMs);
            rep.PeakMemoryBytes = Math.Max(rep.PeakMemoryBytes, rr.PeakMemoryBytes);
            rep.Metrics.Add(new TestRunMetric(i + 1, name, rr.ElapsedMs, rr.PeakMemoryBytes, rr.Status));
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
                        if (resume)
                        {
                            checkpoint!.Outputs[Path.GetFileName(finalOut)] = new OutputCheckpointEntry
                            { InputSha256 = inputHash, OutputSha256 = FileSha256(finalOut) };
                            if (!SaveOutputCheckpoint(checkpointPath, checkpoint, out string? checkpointError))
                                reason = checkpointError ?? "Không lưu được checkpoint output.";
                        }
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
                if (resume)
                {
                    checkpoint!.Outputs.Remove(Path.GetFileName(finalOut));
                    if (!SaveOutputCheckpoint(checkpointPath, checkpoint, out string? checkpointError) && checkpointError != null)
                        reason += "\n" + checkpointError;
                }
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
