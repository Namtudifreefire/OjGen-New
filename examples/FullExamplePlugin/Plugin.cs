// Example plugin demonstrating all extension points
using System.Text;
using OjGen.Core;

namespace FullExamplePlugin;

public sealed class FullExamplePlugin : IOjGenPlugin
{
    public string Name => "Full Example Plugin";
    public int ApiVersion => ProblemPluginSdk.ApiVersion;
    public string Version => "1.0.0";
    public string Author => "OjGen Team";
    public string Description => "Demonstrates all plugin extension points";
    public string Url => "https://github.com/example/ojgen-plugins";
    public string MinOjGenVersion => "3.3.0";

    public IEnumerable<Problem> CreateProblems()
    {
        yield return new ExampleGraphProblem();
    }

    public IEnumerable<IOjGenValidator>? CreateValidators()
    {
        yield return new ExampleGraphValidator();
    }

    public IEnumerable<IOjGenChecker>? CreateCheckers()
    {
        yield return new ExampleToleranceChecker();
    }

    public IEnumerable<IOjGenExporter>? CreateExporters()
    {
        yield return new ExampleCsvExporter();
    }

    public IEnumerable<IOjGenSolutionRunner>? CreateSolutionRunners()
    {
        yield return new ExampleScriptRunner();
    }

    public IEnumerable<IOjGenEventHook>? CreateEventHooks()
    {
        yield return new ExampleEventHook();
    }
}

// ============================================================
// Custom Problem: Graph with special constraints
// ============================================================
public sealed class ExampleGraphProblem : Problem
{
    public override string Id => "example_graph";
    public override string Title => "Example Graph (Plugin)";
    public override string MainCategory => "Đồ thị (Plugin)";
    public override string FormatDescription =>
        "n m\n" +
        "u1 v1\n" +
        "u2 v2\n" +
        "...\n" +
        "um vm\n" +
        "(đồ thị có hướng, không khuyên, có thể không liên thông)";

    public override IReadOnlyList<ParamSpec> Params => new[]
    {
        new ParamSpec { Key = "minN", Label = "Đỉnh tối thiểu", Type = ParamType.Int, Def = 2, Lo = 1, Hi = 100000 },
        new ParamSpec { Key = "maxN", Label = "Đỉnh tối đa", Type = ParamType.Int, Def = 100, Lo = 1, Hi = 100000 },
        new ParamSpec { Key = "minM", Label = "Cạnh tối thiểu", Type = ParamType.Int, Def = 1, Lo = 0, Hi = 500000 },
        new ParamSpec { Key = "maxM", Label = "Cạnh tối đa", Type = ParamType.Int, Def = 200, Lo = 0, Hi = 500000 },
        new ParamSpec { Key = "allowDisconnected", Label = "Cho phép không liên thông", Type = ParamType.Bool, Def = 1, Lo = 0, Hi = 1 },
    };

    public override Config DefaultConfig() => base.DefaultConfig();

    public override string? ValidateConfig(Config cfg)
    {
        if (cfg.I("minN") > cfg.I("maxN")) return "minN không được lớn hơn maxN";
        if (cfg.I("minM") > cfg.I("maxM")) return "minM không được lớn hơn maxM";
        long maxEdges = cfg.I("maxN") * (cfg.I("maxN") - 1);
        if (cfg.I("maxM") > maxEdges) return $"maxM vượt quá số cạnh tối đa của đồ thị đơn ({maxEdges})";
        return null;
    }

    public override string Generate(Rng rng, Config cfg)
    {
        int n = (int)rng.Range(cfg.I("minN"), cfg.I("maxN"));
        int maxPossibleEdges = n * (n - 1);
        int m = (int)rng.Range(cfg.I("minM"), Math.Min(cfg.I("maxM"), maxPossibleEdges));

        var edges = new HashSet<(int, int)>();
        while (edges.Count < m)
        {
            int u = (int)rng.Range(1, n);
            int v = (int)rng.Range(1, n);
            if (u != v) edges.Add((u, v));
        }

        var sb = new StringBuilder();
        sb.Append($"{n} {m}\n");
        foreach (var (u, v) in edges)
            sb.Append($"{u} {v}\n");
        return sb.ToString();
    }

    public override string? ValidateInput(string input, Config cfg)
    {
        var err = Problem.SplitInputLines(input, out var lines);
        if (err != null) return err;
        if (lines.Count == 0) return "File rỗng";

        if (lines[0].Length != 2) return "Dòng 1 phải có 2 số: n m";
        if (Problem.ReadIntToken(lines[0][0], 1, 100000, "n", out long n) != null) return "n không hợp lệ";
        if (Problem.ReadIntToken(lines[0][1], 0, 500000, "m", out long m) != null) return "m không hợp lệ";

        if (lines.Count != m + 1) return $"Số dòng cạnh ({lines.Count - 1}) không khớp m = {m}";

        var seen = new HashSet<(long, long)>();
        for (int i = 1; i < lines.Count; i++)
        {
            if (lines[i].Length != 2) return $"Dòng {i + 1}: cạnh phải có 2 đỉnh";
            if (Problem.ReadIntToken(lines[i][0], 1, n, $"u{i}", out long u) != null) return $"Dòng {i + 1}: u không hợp lệ";
            if (Problem.ReadIntToken(lines[i][1], 1, n, $"v{i}", out long v) != null) return $"Dòng {i + 1}: v không hợp lệ";
            if (u == v) return $"Dòng {i + 1}: khuyên không được phép";
            if (!seen.Add((u, v))) return $"Dòng {i + 1}: cạnh trùng lặp";
        }
        return null;
    }
}

// ============================================================
// Custom Validator: Checks if graph is actually connected
// ============================================================
public sealed class ExampleGraphValidator : IOjGenValidator
{
    public string Id => "example.graph-connectivity-validator";
    public string Name => "Graph Connectivity Validator";
    public IReadOnlyList<string> ApplicableProblemIds => new[] { "example_graph", "graph" };

    public string? ValidateInput(string input, Config config, Problem problem)
    {
        var err = problem.ValidateInput(input, config);
        if (err != null) return err; // Basic format validation failed

        // Check if graph is connected (for undirected interpretation)
        var lines = input.Trim().Split('\n');
        var first = lines[0].Split(' ');
        int n = int.Parse(first[0]);
        int m = int.Parse(first[1]);

        var adj = new List<int>[n + 1];
        for (int i = 1; i <= n; i++) adj[i] = new List<int>();

        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(' ');
            int u = int.Parse(parts[0]);
            int v = int.Parse(parts[1]);
            adj[u].Add(v);
            adj[v].Add(u); // Treat as undirected for connectivity
        }

        var visited = new bool[n + 1];
        int count = 0;
        void Dfs(int u)
        {
            visited[u] = true;
            count++;
            foreach (int v in adj[u])
                if (!visited[v]) Dfs(v);
        }
        Dfs(1);

        if (count != n)
            return $"Đồ thị không liên thông ({count}/{n} đỉnh đạt được từ đỉnh 1).";

        return null;
    }
}

// ============================================================
// Custom Checker: Floating-point tolerance
// ============================================================
public sealed class ExampleToleranceChecker : IOjGenChecker
{
    public string Id => "example.floating-point-checker";
    public string Name => "Floating Point Tolerance Checker";
    public IReadOnlyList<string> ApplicableProblemIds => new[] { "custom", "array", "queries" };
    public string ArgumentsTemplate => "{input} {output} {answer}";
    public int TimeoutMs => 1000;

    public (bool Accepted, string Message, int ExitCode) Check(
        string inputPath, string outputPath, string answerPath, Config config, Problem problem)
    {
        try
        {
            string output = File.ReadAllText(outputPath).Trim();
            string answer = File.ReadAllText(answerPath).Trim();

            // Tokenize and compare with tolerance
            var outTokens = output.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            var ansTokens = answer.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

            if (outTokens.Length != ansTokens.Length)
                return (false, $"Số token khác nhau: output={outTokens.Length}, answer={ansTokens.Length}", 1);

            const double EPS = 1e-6;
            for (int i = 0; i < outTokens.Length; i++)
            {
                if (double.TryParse(outTokens[i], out double outVal) &&
                    double.TryParse(ansTokens[i], out double ansVal))
                {
                    if (Math.Abs(outVal - ansVal) > EPS * Math.Max(1.0, Math.Abs(ansVal)))
                        return (false, $"Token {i + 1}: output={outVal}, answer={ansVal} (sai số > 1e-6)", 1);
                }
                else if (!string.Equals(outTokens[i], ansTokens[i], StringComparison.Ordinal))
                {
                    return (false, $"Token {i + 1} không khớp: output='{outTokens[i]}', answer='{ansTokens[i]}'", 1);
                }
            }
            return (true, "Chấp nhận (so sánh với sai số 1e-6)", 0);
        }
        catch (Exception ex)
        {
            return (false, "Lỗi checker: " + ex.Message, 2);
        }
    }
}

// ============================================================
// Custom Exporter: CSV format
// ============================================================
public sealed class ExampleCsvExporter : IOjGenExporter
{
    public string Id => "example.csv-exporter";
    public string FormatName => "CSV Test Data";
    public string ArchiveExtension => ".zip";

    public bool Export(string testDirectory, string outputArchivePath, IJobLog log, out string? error)
    {
        error = null;
        try
        {
            var inputFiles = Directory.GetFiles(testDirectory, "*.inp")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();

            if (inputFiles.Length == 0)
            {
                error = "Không tìm thấy file .inp nào";
                return false;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "ojgen-csv-export-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            // Create metadata.csv
            var metaPath = Path.Combine(tempDir, "metadata.csv");
            using (var metaWriter = new StreamWriter(metaPath, false, new UTF8Encoding(false)))
            {
                metaWriter.WriteLine("test_name,input_size_bytes,has_output");
                foreach (var inp in inputFiles)
                {
                    string name = Path.GetFileNameWithoutExtension(inp);
                    long size = new FileInfo(inp).Length;
                    bool hasOut = File.Exists(Path.ChangeExtension(inp, ".out"));
                    metaWriter.WriteLine($"{name},{size},{hasOut}");
                }
            }

            // Copy all input files
            foreach (var inp in inputFiles)
            {
                string dest = Path.Combine(tempDir, Path.GetFileName(inp));
                File.Copy(inp, dest);
            }

            // Copy output files if exist
            foreach (var inp in inputFiles)
            {
                string outFile = Path.ChangeExtension(inp, ".out");
                if (File.Exists(outFile))
                {
                    string dest = Path.Combine(tempDir, Path.GetFileName(outFile));
                    File.Copy(outFile, dest);
                }
            }

            // Create zip
            System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, outputArchivePath);

            // Cleanup
            Directory.Delete(tempDir, true);

            log.Line($"Xuất CSV: {inputFiles.Length} test -> {outputArchivePath}");
            return true;
        }
        catch (Exception ex)
        {
            error = "Lỗi xuất CSV: " + ex.Message;
            return false;
        }
    }
}

// ============================================================
// Custom Solution Runner: For script files (.js, .ts, .py with custom handling)
// ============================================================
public sealed class ExampleScriptRunner : IOjGenSolutionRunner
{
    public string Id => "example.nodejs-runner";
    public string Name => "Node.js Script Runner";
    public IReadOnlyList<string> HandledExtensions => new[] { ".js", ".mjs", ".ts" };

    public PreparedSolution Prepare(string solutionPath, Settings settings, string workDir,
                                     bool forceRebuild, IJobLog log, CancellationToken ct = default)
    {
        var result = new PreparedSolution();

        // For .js/.mjs - run directly with node
        if (solutionPath.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
            solutionPath.EndsWith(".mjs", StringComparison.OrdinalIgnoreCase))
        {
            string? node = FindOnPath("node");
            if (node == null)
            {
                result.Error = "Không tìm thấy Node.js (node) trong PATH.";
                return result;
            }

            result.Ok = true;
            result.Argv = new List<string> { node, Path.GetFullPath(solutionPath) };
            log.Line($"Chạy Node.js script: {solutionPath}");
            return result;
        }

        // For .ts - compile with ts-node or tsc
        if (solutionPath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
        {
            string? node = FindOnPath("node");
            string? npx = FindOnPath("npx");
            if (node == null || npx == null)
            {
                result.Error = "Cần Node.js và npx để chạy TypeScript.";
                return result;
            }

            string buildDir = Path.Combine(Path.GetFullPath(workDir), "ojgen-ts-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(buildDir);
                string tsConfig = Path.Combine(buildDir, "tsconfig.json");
                File.WriteAllText(tsConfig, @"{""compilerOptions"":{""target"":""ES2020"",""module"":""commonjs"",""strict"":true,""esModuleInterop"":true}}");
                File.Copy(solutionPath, Path.Combine(buildDir, "solution.ts"));

                // Try ts-node first
                string tsNodePath = Path.Combine(Path.GetDirectoryName(npx)!, "ts-node");
                if (File.Exists(tsNodePath + (OperatingSystem.IsWindows() ? ".cmd" : "")))
                {
                    result.Ok = true;
                    result.Argv = new List<string> { node, tsNodePath, Path.Combine(buildDir, "solution.ts") };
                    result.TemporaryDirectory = buildDir;
                    log.Line($"Chạy TypeScript với ts-node: {solutionPath}");
                    return result;
                }

                // Fallback: compile with tsc then run
                var compile = ProcessRunner.Run(new[] { npx, "tsc", "--project", buildDir },
                    null, Path.Combine(buildDir, "compile.log"), Path.Combine(buildDir, "compile.log"),
                    buildDir, new RunLimits { TimeoutMs = 30000, MaxOutputBytes = 32 << 20 }, ct);

                if (compile.Status != RunStatus.Ok || !File.Exists(Path.Combine(buildDir, "solution.js")))
                {
                    result.Error = "Biên dịch TypeScript thất bại.";
                    try { Directory.Delete(buildDir, true); } catch { }
                    return result;
                }

                result.Ok = true;
                result.Compiled = true;
                result.TemporaryDirectory = buildDir;
                result.Argv = new List<string> { node, Path.Combine(buildDir, "solution.js") };
                log.Line($"Biên dịch TypeScript thành công, chạy với Node.js");
                return result;
            }
            catch (Exception ex)
            {
                try { if (Directory.Exists(buildDir)) Directory.Delete(buildDir, true); } catch { }
                result.Error = "Không chuẩn bị được TypeScript: " + ex.Message;
                return result;
            }
        }

        result.Error = "Định dạng không được hỗ trợ: " + Path.GetExtension(solutionPath);
        return result;
    }

    private static string? FindOnPath(string name)
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
                catch { }
            }
        }
        return null;
    }
}

// ============================================================
// Event Hook: Logging example
// ============================================================
public sealed class ExampleEventHook : IOjGenEventHook
{
    private int _totalTests = 0;
    private int _passedTests = 0;
    private long _totalTimeMs = 0;

    public void OnStart(IJobLog log)
    {
        log.Line("[Plugin Hook] OjGen đã khởi động");
    }

    public void OnBeforeGenerate(Problem problem, Config config, int testIndex, int totalTests, IJobLog log)
    {
        if (testIndex == 0)
        {
            _totalTests = totalTests;
            log.Line($"[Plugin Hook] Bắt đầu sinh {totalTests} test cho {problem.Id}");
        }
    }

    public void OnAfterGenerate(Problem problem, Config config, int testIndex, int totalTests, string input, IJobLog log)
    {
        if (testIndex % 10 == 0 || testIndex == totalTests - 1)
        {
            log.Line($"[Plugin Hook] Đã sinh test {testIndex + 1}/{totalTests} ({input.Length} bytes)");
        }
    }

    public void OnBeforeRunSolution(Problem problem, Config config, string inputPath, int testIndex, IJobLog log)
    {
        // Could log timing, memory, etc.
    }

    public void OnAfterRunSolution(Problem problem, Config config, string inputPath, int testIndex, RunResult result, IJobLog log)
    {
        _totalTimeMs += result.ElapsedMs;
        if (result.Status == RunStatus.Ok) _passedTests++;
    }

    public void OnBeforeRunChecker(Problem problem, string checkerId, string inputPath, string outputPath, string answerPath, IJobLog log)
    {
        log.Line($"[Plugin Hook] Chạy checker {checkerId} cho {Path.GetFileName(inputPath)}");
    }

    public void OnAfterRunChecker(Problem problem, string checkerId, string inputPath, string outputPath, string answerPath, bool accepted, string message, IJobLog log)
    {
        log.Line($"[Plugin Hook] Checker {checkerId}: {(accepted ? "AC" : "WA")} - {message}");
    }

    public void OnAuditStart(Problem problem, Config config, int testCount, IJobLog log)
    {
        log.Line($"[Plugin Hook] Bắt đầu audit {testCount} test");
        _passedTests = 0;
        _totalTimeMs = 0;
    }

    public void OnAuditComplete(Problem problem, IOjGenAuditReport report, IJobLog log)
    {
        log.Line($"[Plugin Hook] Audit hoàn tất: {report.PassedCount}/{report.TotalCount} đạt, {report.FailedCount} lỗi");
    }

    public void OnBeforePackage(string testDirectory, string zipPath, IJobLog log)
    {
        log.Line($"[Plugin Hook] Đang tạo package: {zipPath}");
    }

    public void OnAfterPackage(string testDirectory, string zipPath, bool success, IJobLog log)
    {
        log.Line($"[Plugin Hook] Package {(success ? "thành công" : "thất bại")}: {zipPath}");
    }

    public void OnShutdown(IJobLog log)
    {
        log.Line($"[Plugin Hook] OjGen đang tắt. Tổng test: {_totalTests}, Đạt: {_passedTests}, Tổng thời gian: {_totalTimeMs}ms");
    }
}