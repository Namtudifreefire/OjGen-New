using System.Text;

namespace OjGen.Core;

public sealed record ValidatorContractResult(string Name, bool Passed, string Detail);
public sealed class ValidatorContractRunReport
{
    public List<ValidatorContractResult> Cases { get; } = new();
    public string Error { get; set; } = "";
    public int Passed => Cases.Count(item => item.Passed);
    public int Failed => Cases.Count - Passed;
}

public static class ValidatorContractRunner
{
    public static ValidationContractSuite CreateForProblem(Problem problem, Config config)
    {
        var suite = new ValidationContractSuite();
        string valid = problem.Generate(new Rng(20261101), config.Clone());
        if (problem.ValidateInput(valid, config) is { } error) throw new InvalidDataException("Generator tạo mẫu không hợp lệ: " + error);
        suite.ValidatorCases.Add(new ValidatorContractCase { Name = "accept-generated-valid", Input = valid, ExpectedAccepted = true });
        var candidates = new List<(string Name, string Input)>
        {
            ("reject-empty", ""),
            ("reject-truncated", Truncate(valid)),
            ("reject-extra-token", valid.TrimEnd('\r', '\n') + "\nOJGEN_EXTRA_TOKEN\n"),
        };
        var seen = new HashSet<string>(StringComparer.Ordinal) { valid };
        foreach (var candidate in candidates)
            if (seen.Add(candidate.Input) && problem.ValidateInput(candidate.Input, config) != null)
                suite.ValidatorCases.Add(new ValidatorContractCase { Name = candidate.Name, Input = candidate.Input, ExpectedAccepted = false });
        return suite;
    }

    private static string Truncate(string value)
    {
        string trimmed = value.TrimEnd('\r', '\n');
        int newline = trimmed.LastIndexOf('\n');
        return newline >= 0 ? trimmed[..(newline + 1)] : trimmed.Length > 0 ? trimmed[..^1] : "x";
    }

    public static ValidatorContractRunReport Run(string validatorPath, ValidationContractSuite suite, Settings settings,
        string temporaryRoot, IJobLog? log = null, CancellationToken cancellationToken = default)
    {
        log ??= NullLog.Instance;
        var report = new ValidatorContractRunReport();
        string fullRoot = Path.GetFullPath(temporaryRoot);
        string runDirectory = Path.Combine(fullRoot, "contract-" + Guid.NewGuid().ToString("N"));
        PreparedSolution? prepared = null;
        try
        {
            Directory.CreateDirectory(runDirectory);
            prepared = SolutionRunner.Prepare(validatorPath, settings, Path.Combine(runDirectory, "build"), false, log, cancellationToken);
            if (!prepared.Ok) { report.Error = prepared.Error; return report; }
            string stdout = Path.Combine(runDirectory, "validator.stdout"), stderr = Path.Combine(runDirectory, "validator.stderr");
            var limits = new RunLimits { TimeoutMs = settings.TimeoutMs, MaxOutputBytes = (ulong)settings.MaxOutputMB << 20, MemoryBytes = (ulong)settings.MemoryMB << 20 };
            for (int i = 0; i < suite.ValidatorCases.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested) { report.Error = "Đã hủy."; break; }
                var test = suite.ValidatorCases[i];
                string input = Path.Combine(runDirectory, "case-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".inp");
                File.WriteAllText(input, test.Input, new UTF8Encoding(false));
                var argv = prepared.Argv.Concat(new[] { input }).ToList();
                var run = ProcessRunner.Run(argv, input, stdout, stderr, prepared.WorkingDirectory.Length > 0 ? prepared.WorkingDirectory : runDirectory, limits, cancellationToken);
                bool accepted = run.Status == RunStatus.Ok || (run.Status == RunStatus.RuntimeError && run.ExitCode == 42);
                bool decisive = accepted || (run.Status == RunStatus.RuntimeError && run.ExitCode != 0);
                bool passed = decisive && accepted == test.ExpectedAccepted;
                string detail = passed ? (accepted ? "Được chấp nhận đúng" : "Bị từ chối đúng") :
                    $"Mong đợi {(test.ExpectedAccepted ? "chấp nhận" : "từ chối")}, thực tế {ProcessRunner.StatusName(run.Status)} (exit {run.ExitCode}).";
                report.Cases.Add(new ValidatorContractResult(test.Name, passed, detail));
                log.Progress("Validator contract", i + 1, suite.ValidatorCases.Count);
            }
        }
        catch (Exception ex) { report.Error = "Không chạy được validator: " + ex.Message; }
        finally
        {
            if (prepared != null) SolutionRunner.Cleanup(prepared);
            try
            {
                string fullTarget = Path.GetFullPath(runDirectory);
                if (string.Equals(Path.GetDirectoryName(fullTarget), fullRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullTarget))
                    Directory.Delete(fullTarget, true);
            }
            catch { }
        }
        return report;
    }
}
