namespace OjGen.Core;

/// <summary>Shared configuration and validation helpers for GUI and CLI audits.</summary>
public static class ReleaseAuditCommon
{
    public static Config ConfigFor(Config fallback, JobSpec? spec, int testIndex)
    {
        if (spec == null) return fallback;
        if (testIndex < spec.ManualTests.Count) return spec.ManualTests[testIndex].Cfg;
        int index = testIndex - spec.ManualTests.Count;
        if (spec.Groups.Count == 0) return spec.Cfg;
        foreach (var group in spec.Groups)
        {
            if (index < group.TestCount) return group.Cfg;
            index -= group.TestCount;
        }
        return fallback;
    }

    public static List<string> InvalidVariants(string input, Problem problem, Config config)
    {
        var candidates = new List<string> { "", input + "OJGEN_INVALID_TRAILING_TOKEN\n", "OJGEN_INVALID\n" };
        string trimmed = input.TrimEnd('\r', '\n');
        int lastWhitespace = trimmed.LastIndexOfAny(new[] { ' ', '\t', '\r', '\n' });
        if (lastWhitespace >= 0) candidates.Add(trimmed[..(lastWhitespace + 1)].TrimEnd('\r', '\n') + "\n");
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        return candidates.Where(candidate => distinct.Add(candidate) && problem.ValidateInput(candidate, config) != null).Take(3).ToList();
    }

    public static RunLimits Limits(Settings settings) => new()
    {
        TimeoutMs = Math.Clamp(settings.TimeoutMs, 1, 3600000),
        MaxOutputBytes = (ulong)Math.Clamp(settings.MaxOutputMB, 1, 4096) << 20,
        MemoryBytes = settings.MemoryMB <= 0 ? 0UL : (ulong)Math.Min(settings.MemoryMB, 1048576) << 20,
    };
}
