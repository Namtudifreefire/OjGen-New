// Problem.cs - Giao diện thống nhất cho mọi dạng bài (module) sinh test.
//
// Mỗi module cài đặt một lớp con của Problem. Phần lõi chỉ làm việc qua giao diện này:
//   Params          -> danh sách tham số (form trên giao diện và dòng lệnh --key value)
//   ValidateConfig  -> kiểm tra cấu hình trước khi sinh
//   Generate        -> sinh nội dung MỘT file input
//   ValidateInput   -> kiểm tra lại file input đúng định dạng và đúng giới hạn
// Các hàm kiểm tra trả về null nếu hợp lệ, ngược lại là thông báo lỗi.
using System.Text;

namespace OjGen.Core;

public abstract class Problem
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    /// <summary>Nhóm cấu trúc input chính, dùng để chia nhóm các nhánh trong GUI.</summary>
    public virtual string MainCategory => "Khác";
    public abstract string FormatDescription { get; }
    public abstract IReadOnlyList<ParamSpec> Params { get; }

    /// <summary>Cấu hình mặc định lấy từ Params.</summary>
    public virtual Config DefaultConfig()
    {
        var c = new Config();
        foreach (var p in Params)
        {
            if (p.Type == ParamType.Text) c.SetText(p.Key, p.DefText);
            else c.Set(p.Key, p.Def);
        }
        return c;
    }

    /// <summary>Hoàn thiện cấu hình từ dòng lệnh (vd suy ra n từ --bounds). explicitKeys là các tham số người dùng đã truyền.</summary>
    public virtual void FinalizeCliConfig(Config cfg, ISet<string> explicitKeys) { }

    /// <summary>
    /// Giao diện gọi sau mỗi lần người dùng sửa một tham số, để module tự đồng bộ các tham số liên quan
    /// (vd custom: dán bounds thì n tự bằng số vị trí). Mặc định không làm gì.
    /// </summary>
    public virtual void AdjustConfig(Config cfg, string changedKey) { }

    /// <summary>Kiểm tra logic giữa các tham số. Miền [Lo, Hi] của từng tham số đã được kiểm tra trước.</summary>
    public abstract string? ValidateConfig(Config cfg);

    /// <summary>Sinh nội dung một file input (kết thúc bằng '\n', xuống dòng kiểu LF).</summary>
    public abstract string Generate(Rng rng, Config cfg);

    /// <summary>Xác thực một file input theo đúng định dạng và giới hạn của cấu hình.</summary>
    public abstract string? ValidateInput(string input, Config cfg);

    /// <summary>Ước lượng dung lượng tối đa của một file input (byte), 0 = không rõ.</summary>
    public virtual ulong EstimateMaxBytes(Config cfg) => 0;

    /// <summary>Mô tả cấu hình (ghi vào file thông tin và hiển thị cho người dùng).</summary>
    public virtual string DescribeConfig(Config cfg)
    {
        var sb = new StringBuilder();
        foreach (var p in Params)
        {
            sb.Append("  - ").Append(p.Label);
            if (!p.Label.Contains("(" + p.Key + ")")) sb.Append(" (").Append(p.Key).Append(')');
            sb.Append(": ");
            if (p.Type == ParamType.Text) sb.Append('"').Append(cfg.T(p.Key)).Append('"');
            else if (p.Type == ParamType.Bool) sb.Append(cfg.B(p.Key) ? "có" : "không");
            else sb.Append(Util.LL(cfg.I(p.Key)));
            sb.Append('\n');
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------ hỗ trợ chung

    /// <summary>Kiểm tra miền giá trị của từng tham số theo ParamSpec.</summary>
    public static string? CheckParamRanges(Problem p, Config cfg)
    {
        foreach (var ps in p.Params)
        {
            if (ps.Type == ParamType.Int)
            {
                long v = cfg.I(ps.Key);
                if (v < ps.Lo || v > ps.Hi)
                    return $"Tham số '{ps.Key}' ({ps.Label}) = {Util.LL(v)} nằm ngoài miền cho phép [{Util.LL(ps.Lo)}, {Util.LL(ps.Hi)}].";
            }
            else if (ps.Type == ParamType.Bool)
            {
                long v = cfg.I(ps.Key);
                if (v != 0 && v != 1) return $"Tham số '{ps.Key}' chỉ nhận 0 hoặc 1.";
            }
        }
        return null;
    }

    /// <summary>
    /// Tách input thành các dòng, mỗi dòng là danh sách token. Bắt buộc: kết thúc bằng '\n',
    /// không có '\r', không có dòng trống, các token cách nhau đúng MỘT dấu cách,
    /// không có khoảng trắng đầu/cuối dòng.
    /// </summary>
    public static string? SplitInputLines(string input, out List<string[]> lines)
    {
        lines = new List<string[]>();
        if (input.Length == 0) return "File input rỗng.";
        if (input[^1] != '\n') return "File input không kết thúc bằng ký tự xuống dòng.";
        int start = 0, lineNo = 0;
        while (start < input.Length)
        {
            int end = input.IndexOf('\n', start);
            string line = input.Substring(start, end - start);
            lineNo++;
            string where = $"Dòng {lineNo}: ";
            if (line.Length == 0) return where + "dòng trống không hợp lệ.";
            if (line.Contains('\r')) return where + "chứa ký tự \\r (CRLF).";
            if (line[0] == ' ' || line[^1] == ' ') return where + "có khoảng trắng thừa ở đầu/cuối dòng.";
            if (line.Contains("  ")) return where + "có hai dấu cách liên tiếp.";
            if (line.Contains('\t')) return where + "chứa ký tự tab.";
            lines.Add(line.Split(' '));
            start = end + 1;
        }
        return null;
    }

    /// <summary>Đọc token số nguyên và kiểm tra nằm trong [lo, hi].</summary>
    public static string? ReadIntToken(string tok, long lo, long hi, string what, out long value)
    {
        if (!Util.ParseStrictToken(tok, out value)) return $"{what}: '{tok}' không phải số nguyên hợp lệ.";
        if (value < lo || value > hi) return $"{what} = {tok} nằm ngoài [{Util.LL(lo)}, {Util.LL(hi)}].";
        return null;
    }

    /// <summary>Sinh k giá trị đôi một khác nhau trong [lo, hi] (yêu cầu hi - lo + 1 &gt;= k).</summary>
    public static List<long> DistinctValues(Rng rng, long lo, long hi, int k)
    {
        var res = new List<long>(k);
        ulong span = Util.SpanOf(lo, hi);
        if (span != ulong.MaxValue && span + 1 <= 4UL * (ulong)k + 16)
        {
            // Miền nhỏ: xáo trộn một phần (Fisher-Yates) trên toàn bộ miền.
            var all = new long[(int)(span + 1)];
            for (int i = 0; i < all.Length; i++) all[i] = unchecked((long)((ulong)lo + (ulong)i));
            for (int i = 0; i < k; i++)
            {
                int j = i + (int)rng.Below((ulong)(all.Length - i));
                (all[i], all[j]) = (all[j], all[i]);
                res.Add(all[i]);
            }
            return res;
        }
        var seen = new HashSet<long>(k * 2);
        while (res.Count < k)
        {
            long v = rng.Range(lo, hi);
            if (seen.Add(v)) res.Add(v);
        }
        return res;
    }
}

/// <summary>Danh sách các dạng bài. Thêm module mới: tạo lớp con của Problem rồi thêm vào All.</summary>
public static class Problems
{
    private static readonly object Sync = new();
    private static readonly List<Problem> BuiltIns = new()
    {
        new SingleNProblem(),
        new TwoIntsProblem(),
        new ArrayProblem(),
        new QueriesProblem(),
        new PrefixSumRangeProblem(),
        new TdcPrefixSumProblem(),
        new StringProblem(),
        new MatrixProblem(),
        new GridProblem(),
        new PairListProblem(),
        new GraphProblem(),
        new TreeProblem(),
        new MultiTestProblem(),
        new CustomProblem(),
        new ChessMovesProblem(),
        new ChessPieceMovesProblem(),
        new ChessKnightTargetsProblem(),
        new ChessKnightReachabilityProblem(),
        new ChessKnightShortestPathProblem(),
        // new MyProblem(),   // <- thêm module mới tại đây
    };

    public static IReadOnlyList<Problem> All
    {
        get { lock (Sync) return BuiltIns.ToArray(); }
    }

    public static bool Register(Problem problem) => RegisterMany(new[] { problem }, out _);

    public static bool RegisterMany(IEnumerable<Problem> problems, out string? error)
    {
        error = null;
        if (problems == null) { error = "Danh sách dạng bài rỗng."; return false; }
        var incoming = problems.ToList();
        lock (Sync)
        {
            var ids = new HashSet<string>(BuiltIns.Select(problem => problem.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var problem in incoming)
            {
                if (problem == null || string.IsNullOrWhiteSpace(problem.Id))
                { error = "ID dạng bài rỗng."; return false; }
                if (!ids.Add(problem.Id))
                { error = "ID dạng bài bị trùng: " + problem.Id; return false; }
            }
            BuiltIns.AddRange(incoming);
            return true;
        }
    }

    public static Problem? Find(string id)
    {
        lock (Sync)
            return BuiltIns.FirstOrDefault(problem => string.Equals(problem.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Plugin contract: return one or more Problem implementations from an extension assembly.</summary>
public interface IOjGenProblemPlugin
{
    string Name { get; }
    IEnumerable<Problem> CreateProblems();
}

/// <summary>Optional metadata interface for plugins that declare their OjGen extension API compatibility.</summary>
public interface IOjGenProblemPluginMetadata
{
    int ApiVersion { get; }
    /// <summary>Plugin version (semantic versioning recommended).</summary>
    string Version { get; }
    /// <summary>Plugin author.</summary>
    string Author { get; }
    /// <summary>Brief description of what this plugin provides.</summary>
    string Description { get; }
    /// <summary>Plugin homepage or repository URL.</summary>
    string Url { get; }
    /// <summary>Minimum OjGen version required (e.g., "3.3.0").</summary>
    string MinOjGenVersion { get; }
}

/// <summary>Extended plugin interface supporting multiple extension points.</summary>
public interface IOjGenPlugin : IOjGenProblemPlugin, IOjGenProblemPluginMetadata
{
    /// <summary>Custom validators (optional).</summary>
    IEnumerable<IOjGenValidator>? CreateValidators() => null;

    /// <summary>Custom checkers (optional).</summary>
    IEnumerable<IOjGenChecker>? CreateCheckers() => null;

    /// <summary>Custom test package exporters (optional).</summary>
    IEnumerable<IOjGenExporter>? CreateExporters() => null;

    /// <summary>Custom solution runners (optional).</summary>
    IEnumerable<IOjGenSolutionRunner>? CreateSolutionRunners() => null;

    /// <summary>Event hooks (optional).</summary>
    IEnumerable<IOjGenEventHook>? CreateEventHooks() => null;
}

/// <summary>Custom validator that can validate an input file against problem constraints.</summary>
public interface IOjGenValidator
{
    /// <summary>Unique validator ID (e.g., "myplugin.graph-validator").</summary>
    string Id { get; }

    /// <summary>Human-readable name.</summary>
    string Name { get; }

    /// <summary>Problem IDs this validator applies to (empty = all problems).</summary>
    IReadOnlyList<string> ApplicableProblemIds { get; }

    /// <summary>Validate an input file. Returns null if valid, error message if invalid.</summary>
    string? ValidateInput(string input, Config config, Problem problem);
}

/// <summary>Custom checker for comparing output with expected answer.</summary>
public interface IOjGenChecker
{
    /// <summary>Unique checker ID (e.g., "myplugin.floating-point-checker").</summary>
    string Id { get; }

    /// <summary>Human-readable name.</summary>
    string Name { get; }

    /// <summary>Problem IDs this checker applies to (empty = all problems).</summary>
    IReadOnlyList<string> ApplicableProblemIds { get; }

    /// <summary>Checker arguments template (e.g., "{input} {output} {answer}").</summary>
    string ArgumentsTemplate { get; }

    /// <summary>Timeout for checker execution in milliseconds.</summary>
    int TimeoutMs { get; }

    /// <summary>Check if candidate output matches expected answer.
    /// Returns: (isAccepted, message, exitCode) where exitCode: 0=AC, 1=WA, 2=checker error.</summary>
    (bool Accepted, string Message, int ExitCode) Check(string inputPath, string outputPath, string answerPath, Config config, Problem problem);
}

/// <summary>Custom test package exporter for interchange formats.</summary>
public interface IOjGenExporter
{
    /// <summary>Unique exporter ID (e.g., "myplugin.custom-format").</summary>
    string Id { get; }

    /// <summary>Human-readable format name.</summary>
    string FormatName { get; }

    /// <summary>File extension for the exported archive (e.g., ".zip", ".tar.gz").</summary>
    string ArchiveExtension { get; }

    /// <summary>Export test data to the specified archive.</summary>
    bool Export(string testDirectory, string outputArchivePath, IJobLog log, out string? error);
}

/// <summary>Custom solution runner for compiling/running solutions in unsupported languages.</summary>
public interface IOjGenSolutionRunner
{
    /// <summary>Unique runner ID (e.g., "myplugin.haskell-runner").</summary>
    string Id { get; }

    /// <summary>Human-readable name.</summary>
    string Name { get; }

    /// <summary>File extensions this runner handles (e.g., [".hs", ".lhs"]).</summary>
    IReadOnlyList<string> HandledExtensions { get; }

    /// <summary>Prepare a solution for execution. Returns PreparedSolution or error.</summary>
    PreparedSolution Prepare(string solutionPath, Settings settings, string workDir, bool forceRebuild, IJobLog log, CancellationToken ct = default);
}

/// <summary>Minimal audit report interface for event hooks (CLI implements this).</summary>
public interface IOjGenAuditReport
{
    bool Cancelled { get; }
    bool HasErrors { get; }
    int WarningCount { get; }
    int TotalCount { get; }
    int PassedCount { get; }
    int FailedCount { get; }
    IReadOnlyList<IOjGenAuditItem> Items { get; }
}

/// <summary>Minimal audit item interface for event hooks.</summary>
public interface IOjGenAuditItem
{
    string Level { get; } // "Pass", "Info", "Warning", "Error"
    string Title { get; }
    string Detail { get; }
}

/// <summary>Event hook for plugin lifecycle and generation events.</summary>
public interface IOjGenEventHook
{
    /// <summary>Called when OjGen starts (CLI or GUI).</summary>
    void OnStart(IJobLog log);

    /// <summary>Called before generating a test case.</summary>
    void OnBeforeGenerate(Problem problem, Config config, int testIndex, int totalTests, IJobLog log);

    /// <summary>Called after generating a test case.</summary>
    void OnAfterGenerate(Problem problem, Config config, int testIndex, int totalTests, string input, IJobLog log);

    /// <summary>Called before running a solution on a test.</summary>
    void OnBeforeRunSolution(Problem problem, Config config, string inputPath, int testIndex, IJobLog log);

    /// <summary>Called after running a solution on a test.</summary>
    void OnAfterRunSolution(Problem problem, Config config, string inputPath, int testIndex, RunResult result, IJobLog log);

    /// <summary>Called before running a checker.</summary>
    void OnBeforeRunChecker(Problem problem, string checkerId, string inputPath, string outputPath, string answerPath, IJobLog log);

    /// <summary>Called after running a checker.</summary>
    void OnAfterRunChecker(Problem problem, string checkerId, string inputPath, string outputPath, string answerPath, bool accepted, string message, IJobLog log);

    /// <summary>Called when audit is about to start.</summary>
    void OnAuditStart(Problem problem, Config config, int testCount, IJobLog log);

    /// <summary>Called when audit completes.</summary>
    void OnAuditComplete(Problem problem, IOjGenAuditReport report, IJobLog log);

    /// <summary>Called when a test package is about to be created.</summary>
    void OnBeforePackage(string testDirectory, string zipPath, IJobLog log);

    /// <summary>Called after a test package is created.</summary>
    void OnAfterPackage(string testDirectory, string zipPath, bool success, IJobLog log);

    /// <summary>Called when OjGen shuts down.</summary>
    void OnShutdown(IJobLog log);
}

public static class ProblemPluginSdk
{
    /// <summary>Increment only for a breaking change to the public extension contract.</summary>
    public const int ApiVersion = 2;
}
