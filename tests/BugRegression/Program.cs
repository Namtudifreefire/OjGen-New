using OjGen.Core;
using OjGen.Gui;
using OjGen.Cli;
using System.Reflection;
using System.Text.Json.Nodes;

static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.Error.WriteLine("OK: " + name); }
var html = new ProblemAuthoringDocument { Description = "$1 <= n & n > 0$ `$HOME` `$x$` `<b>` $10^5$" }.ToHtml();
Check(html.Contains("1 &lt;= n &amp; n &gt; 0") && !html.Contains("&amp;lt;"), "math encoded once");
Check(html.Contains("<code>$HOME</code>") && html.Contains("<code>$x$</code>") && html.Contains("<code>&lt;b&gt;</code>") && html.Contains("10<sup>5</sup>"), "literal code and math");
string dir = Path.Combine(Path.GetTempPath(), "ojgen-regression-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
try {
    File.WriteAllText(Path.Combine(dir, "a.inp"), "5\n");
    File.WriteAllText(Path.Combine(dir, "b.inp"), "50\n");
    var root = JsonNode.Parse("""{"groups":[{"Name":"small","ints":{"minVal":1,"maxVal":10}}],"manualTests":[{"ConfigName":"manual","ints":{"minVal":40,"maxVal":60}}],"tests":[{"name":"a","group":"small"},{"name":"b","group":"manual"}]}""")!;
    var problem = new SingleNProblem(); var fallback = problem.DefaultConfig(); fallback.Set("maxVal", 1);
    foreach (bool camel in new[] { false, true }) {
        if (camel) { var g = root["groups"]![0]!; g["name"] = g["Name"]!.GetValue<string>(); g.AsObject().Remove("Name"); var m = root["manualTests"]![0]!; m["configName"] = m["ConfigName"]!.GetValue<string>(); m.AsObject().Remove("ConfigName"); }
        File.WriteAllText(Path.Combine(dir, "ojgen_provenance.json"), root.ToJsonString());
        Check(TestInventoryService.Validate(dir, problem, fallback).All(row => row.Validation == "Hợp lệ"), camel ? "camelCase provenance" : "legacy PascalCase provenance");
    }
} finally { Directory.Delete(dir, true); }
var logger = new ConsoleLog();
typeof(ConsoleLog).GetField("_live", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(logger, true);
var original = Console.Out; using var capture = new StringWriter(); Console.SetOut(capture);
try { logger.Progress("Sinh input · Nhỏ", 10, 11); logger.Progress("Sinh input · Nhỏ", 11, 11); logger.Progress("Sinh input khác", 10, 10); } finally { Console.SetOut(original); }
Check(capture.ToString().Contains("10/11") && capture.ToString().Contains("11/11") && !capture.ToString().Contains("10/10"), "CLI progress for group");
var graph = new GraphProblem();
foreach (long simple in new long[] { 0, 1 }) {
    var cfg = graph.DefaultConfig();
    cfg.Set("minN", 1); cfg.Set("maxN", 1); cfg.Set("minM", 0); cfg.Set("maxM", 10);
    cfg.Set("directed", 1); cfg.Set("stronglyConnected", 1); cfg.Set("simple", simple);
    Check(graph.ValidateConfig(cfg) == null, "one vertex graph config " + simple);
    var generated = graph.Generate(new Rng(42), cfg);
    Check(generated.StartsWith("1 0\n") && graph.ValidateInput(generated, cfg) == null, "one vertex graph generation " + simple);
    cfg.Set("minM", 1);
    Check(graph.ValidateConfig(cfg) != null, "one vertex positive edge count rejected " + simple);
    if (simple == 1) foreach (long density in new long[] { 1, 2, 3 }) {
        cfg.Set("density", density);
        Check(graph.ValidateConfig(cfg) == null && graph.ValidateInput(graph.Generate(new Rng(42), cfg), cfg) == null, "one vertex density " + density);
    }
}

string scenarioDir = Path.Combine(Path.GetTempPath(), "ojgen-profile-regression-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(scenarioDir);
try {
    var runDir = Path.Combine(scenarioDir, "cwd"); Directory.CreateDirectory(runDir);
    File.WriteAllText(Path.Combine(runDir, "marker.txt"), "ready");
    File.WriteAllText(Path.Combine(scenarioDir, "reference.py"), "import pathlib,sys\nassert pathlib.Path('marker.txt').read_text() == 'ready'\nprint(1)\n");
    File.WriteAllText(Path.Combine(scenarioDir, "candidate.py"), "import pathlib,sys\nassert pathlib.Path('marker.txt').read_text() == 'ready'\nprint(2)\n");
    File.WriteAllText(Path.Combine(scenarioDir, "checker.py"), "import pathlib,sys\nassert pathlib.Path('marker.txt').read_text() == 'ready'\nsys.exit(1 if pathlib.Path(sys.argv[2]).read_text().startswith('OJGEN_INVALID_OUTPUT') else 0)\n");
    string MakeProfile(string script) {
        var profile = new SolutionRunProfile { RunExecutable = OperatingSystem.IsWindows() ? "python" : "python3", RunArguments = new() { "{sourceDir}/" + script }, WorkingDirectory = "cwd" };
        string path = Path.Combine(scenarioDir, script + ".ojrun.json"); File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(profile)); return path;
    }
    string reference = MakeProfile("reference.py"), candidate = MakeProfile("candidate.py"), checker = MakeProfile("checker.py");
    var settings = new Settings { OutputRoot = scenarioDir, TimeoutMs = 5000 };
    var single = new SingleNProblem(); var cfg = single.DefaultConfig();
    var diff = DifferentialTester.Run(new DifferentialSpec { Problem = single, Cfg = cfg, TestCount = 1, Seed = 12, ReferencePath = reference, CandidatePath = candidate, MaxMinimizationTrials = 10 }, settings, NullLog.Instance);
    Check(diff.Mismatches == 1 && diff.Failures.Count == 1 && diff.Failures[0].MinimizationTrials > 0 && diff.Error.Length == 0, "run profile cwd for differential and shrinker");
    string input = Path.Combine(scenarioDir, "test.inp"); File.WriteAllText(input, "1\n"); File.WriteAllText(Path.ChangeExtension(input, ".out"), "1\n");
    var checkedResult = CheckerAuditRunner.Run(checker, "{input} {output}", new[] { input }, settings, scenarioDir, NullLog.Instance);
    Check(checkedResult.Ok && checkedResult.MalformedProbeRejected, "run profile cwd for checker and probe");
    var prepared = SolutionRunner.Prepare(reference, settings, Path.Combine(scenarioDir, "build"), false, NullLog.Instance);
    Check(prepared.Ok, "prepare checkpoint fixture");
    Directory.CreateDirectory(Path.Combine(scenarioDir, ".ojgen_output_checkpoint.json"));
    var checkpointLog = new StringLog();
    var checkpointResult = SolutionRunner.ProduceOutputs(prepared.Argv, new[] { input }, settings, scenarioDir, checkpointLog,
        resume: true, resumeFingerprint: "checkpoint-test", solutionWorkingDirectory: prepared.WorkingDirectory);
    Check(checkpointResult.Ok == 0 && checkpointResult.Failures.Count == 1 && !checkpointLog.ToString().Contains("... OK"), "checkpoint failure is not counted as success");
    SolutionRunner.Cleanup(prepared);
} finally { Directory.Delete(scenarioDir, true); }

string cppDir = Path.Combine(Path.GetTempPath(), "ojgen-header-regression-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(cppDir);
try {
    string source = Path.Combine(cppDir, "main.cpp"), header = Path.Combine(cppDir, "helper.h");
    File.WriteAllText(source, "#include \"helper.h\"\n#include <iostream>\nint main(){std::cout << VALUE << '\\n';}\n");
    var settings = new Settings { OutputRoot = cppDir, CxxFlags = "-O0" };
    string Execute() {
        var prepared = SolutionRunner.Prepare(source, settings, settings.WorkDir, false, NullLog.Instance);
        Check(prepared.Ok && !prepared.Cached, "C++ rebuilt");
        string stdout = Path.Combine(cppDir, "result.txt"), stderr = Path.Combine(cppDir, "error.txt");
        var run = ProcessRunner.Run(prepared.Argv, null, stdout, stderr, cppDir, new RunLimits { TimeoutMs = 5000 }, default);
        Check(run.Status == RunStatus.Ok, "C++ execution");
        return File.ReadAllText(stdout).Trim();
    }
    File.WriteAllText(header, "#define VALUE 11\n"); Check(Execute() == "11", "first header value");
    File.WriteAllText(header, "#define VALUE 22\n"); Check(Execute() == "22", "changed header value");
} finally { Directory.Delete(cppDir, true); }

static string[][] Rows(params string[] rows) => rows.Select(row => row.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToArray();
Check(TestVisualizationDetector.Detect(Rows("2 4", ".#S.", "..T#")) == "Lưới", "compact grid detection");
Check(TestVisualizationDetector.Detect(Rows("3 2", "1 2", "2 3")) == "Đồ thị / cây", "graph detection");
Check(TestVisualizationDetector.Detect(Rows("2 3", ". # S", "# . T")) == "Lưới", "spaced grid detection");
foreach (string tag in new[] { "euler-tour (đường đi Euler trên cây)", "mo-tree (Mo trên cây)" })
    Check(AlgorithmTopics.All.Any(item => item.Name == tag && item.RecommendedProblemId == "tree"), "tree topic " + tag);
var matrix = new MatrixProblem(); var matrixCfg = matrix.DefaultConfig(); matrixCfg.Set("layout", 2); matrixCfg.Set("maxN", 10_000_000);
Check(matrix.ValidateConfig(matrixCfg) == null, "two arrays cell limit accepted"); matrixCfg.Set("maxN", 10_000_001);
Check(matrix.ValidateConfig(matrixCfg) != null, "two arrays cell limit rejected");
var graphConstraints = new GraphProblem(); var graphCfg = graphConstraints.DefaultConfig(); graphCfg.Set("weighted", 1); graphCfg.Set("minW", -5); graphCfg.Set("maxW", 5); graphCfg.Set("maxM", 99);
var weightCheck = ConstraintConsistencyChecker.Check("|w| <= 10", graphConstraints, graphCfg);
Check(weightCheck.ParsedBounds == 1 && weightCheck.Errors.Count == 0, "graph weight mapped to weights");
graphCfg.Set("maxW", 11); Check(ConstraintConsistencyChecker.Check("|w| <= 10", graphConstraints, graphCfg).Errors.Count > 0, "graph weight violation");
var gridProblem = new GridProblem(); var gridCfg = gridProblem.DefaultConfig(); gridCfg.Set("maxM", 20);
Check(ConstraintConsistencyChecker.Check("w <= 10", gridProblem, gridCfg).Errors.Count > 0, "grid width constraint");
string dataRoot = Path.Combine(Path.GetTempPath(), "ojgen-more-regression-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dataRoot);
try {
    string stringInput = Path.Combine(dataRoot, "string.inp"); File.WriteAllText(stringInput, "abc\n");
    var stringProblem = new StringProblem(); var strCfg = stringProblem.DefaultConfig(); strCfg.Set("minLen", 1); strCfg.Set("maxLen", 10);
    var strReport = CoverageAnalyzer.Analyze(stringProblem, new[] { stringInput }, new[] { new TestGroupSpec { Name="g", TestCount=1, Cfg=strCfg } }, Array.Empty<ManualTestSpec>());
    Check(strReport.Observations.Any(x => x.Contains("Phạm vi len:") && x.Contains("[3, 3]")), "string coverage length");
    string prefixInput = Path.Combine(dataRoot, "prefix.inp"); File.WriteAllText(prefixInput, "3\n1 2 3\n2\n1 2\n1 3\n");
    var prefixProblem = new PrefixSumRangeProblem(); var prefixCfg = prefixProblem.DefaultConfig();
    var prefixReport = CoverageAnalyzer.Analyze(prefixProblem, new[] { prefixInput }, new[] { new TestGroupSpec { Name="g", TestCount=1, Cfg=prefixCfg } }, Array.Empty<ManualTestSpec>());
    Check(prefixReport.Observations.Any(x => x.Contains("Phạm vi q:") && x.Contains("[2, 2]")), "prefix query coverage");
    string source = Path.Combine(dataRoot, "source"), dest = Path.Combine(dataRoot, "import"); Directory.CreateDirectory(source);
    foreach (string n in new[] { "1", "2", "10" }) { File.WriteAllText(Path.Combine(source, n + ".in"), n); File.WriteAllText(Path.Combine(source, n + ".ans"), n); }
    TestPackageTools.Import(source, dest);
    Check(File.ReadAllText(Path.Combine(dest, "test01.inp")) == "1" && File.ReadAllText(Path.Combine(dest, "test02.inp")) == "2" && File.ReadAllText(Path.Combine(dest, "test03.inp")) == "10", "natural import order");
    string zip = Path.Combine(dataRoot, "out.zip"); TestPackageTools.ExportTestData(source, zip, "ojgen");
    using var archive = System.IO.Compression.ZipFile.OpenRead(zip);
    using var reader = new StreamReader(archive.GetEntry("tests/test002.inp")!.Open()); Check(reader.ReadToEnd() == "2", "natural export order");
} finally { Directory.Delete(dataRoot, true); }
