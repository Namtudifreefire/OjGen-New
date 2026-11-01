using System.Text.RegularExpressions;
using OjGen.Core;

namespace OjGen.Gui;

internal sealed class ConstraintCheckResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public int ParsedBounds { get; set; }
}

/// <summary>Checks simple numeric bounds in statement text against the structured generator ranges.</summary>
internal static class ConstraintConsistencyChecker
{
    private const string Number = @"-?\d+(?:\s*\^\s*\d+)?";
    private static readonly Regex Range = new($@"(?<lo>{Number})\s*(?:<=|≤)\s*(?<var>[A-Za-z][A-Za-z0-9]*)\s*(?:<=|≤)\s*(?<hi>{Number})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Upper = new($@"(?<var>[A-Za-z][A-Za-z0-9]*)\s*(?:<=|≤)\s*(?<hi>{Number})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Lower = new($@"(?<lo>{Number})\s*(?:<=|≤)\s*(?<var>[A-Za-z][A-Za-z0-9]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, (string Min, string Max)> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["n"] = ("minN", "maxN"), ["m"] = ("minM", "maxM"), ["q"] = ("minQ", "maxQ"),
        ["t"] = ("minT", "maxT"), ["len"] = ("minLen", "maxLen"), ["length"] = ("minLength", "maxLength"),
        ["rows"] = ("minN", "maxN"), ["h"] = ("minN", "maxN"),
        ["cols"] = ("minM", "maxM"), ["w"] = ("minM", "maxM"),
    };

    private static bool TryNumber(string text, out long value)
    {
        text = Regex.Replace(text, @"\s+", "");
        int power = text.IndexOf('^');
        if (power > 0 && long.TryParse(text[..power], out long basis) && int.TryParse(text[(power + 1)..], out int exponent) &&
            exponent is >= 0 and <= 18)
        {
            try
            {
                long result = 1;
                for (int i = 0; i < exponent; i++) result = checked(result * basis);
                value = result;
                return true;
            }
            catch (OverflowException) { value = 0; return false; }
        }
        return long.TryParse(text, out value);
    }

    public static ConstraintCheckResult Check(string text, Problem problem, Config fallback,
        IReadOnlyList<Config>? additionalConfigs = null)
    {
        var result = new ConstraintCheckResult();
        var configs = new List<Config> { fallback };
        if (additionalConfigs != null) configs.AddRange(additionalConfigs);
        var bounds = new List<(string Variable, long? Lower, long? Upper)>();
        var occupied = new HashSet<(int Start, int Length)>();
        foreach (Match match in Range.Matches(text))
        {
            if (!TryNumber(match.Groups["lo"].Value, out long lo) || !TryNumber(match.Groups["hi"].Value, out long hi)) continue;
            bounds.Add((match.Groups["var"].Value, lo, hi));
            occupied.Add((match.Index, match.Length));
        }
        foreach (Match match in Upper.Matches(text))
        {
            if (occupied.Any(span => match.Index >= span.Start && match.Index < span.Start + span.Length)) continue;
            if (TryNumber(match.Groups["hi"].Value, out long hi)) bounds.Add((match.Groups["var"].Value, null, hi));
        }
        foreach (Match match in Lower.Matches(text))
        {
            if (occupied.Any(span => match.Index >= span.Start && match.Index < span.Start + span.Length)) continue;
            if (TryNumber(match.Groups["lo"].Value, out long lo)) bounds.Add((match.Groups["var"].Value, lo, null));
        }

        foreach (var bound in bounds)
        {
            if (!Aliases.TryGetValue(bound.Variable, out var keys)) continue;
            var minSpec = problem.Params.FirstOrDefault(param => string.Equals(param.Key, keys.Min, StringComparison.OrdinalIgnoreCase));
            var maxSpec = problem.Params.FirstOrDefault(param => string.Equals(param.Key, keys.Max, StringComparison.OrdinalIgnoreCase));
            if (minSpec == null || maxSpec == null) continue;
            result.ParsedBounds++;
            foreach (Config config in configs)
            {
                long actualMin = config.I(minSpec.Key), actualMax = config.I(maxSpec.Key);
                if (problem is GraphProblem graph && bound.Variable.Equals("m", StringComparison.OrdinalIgnoreCase) &&
                    config.I("density") != 0 && graph.TryGetEffectiveEdgeBounds(config, out long effectiveMin, out long effectiveMax))
                {
                    actualMin = effectiveMin;
                    actualMax = effectiveMax;
                }
                if (bound.Lower.HasValue && actualMin < bound.Lower.Value)
                    result.Errors.Add($"Đề ghi {Util.LL(bound.Lower.Value)} ≤ {bound.Variable}, nhưng cấu hình cho phép {Util.LL(actualMin)}.");
                if (bound.Upper.HasValue && actualMax > bound.Upper.Value)
                    result.Errors.Add($"Đề ghi {bound.Variable} ≤ {Util.LL(bound.Upper.Value)}, nhưng cấu hình có thể sinh {Util.LL(actualMax)}.");
            }
        }
        var distinctErrors = result.Errors.Distinct(StringComparer.Ordinal).ToList();
        result.Errors.Clear();
        result.Errors.AddRange(distinctErrors);
        if (text.Trim().Length > 0 && result.ParsedBounds == 0)
            result.Warnings.Add("Chưa nhận diện được cận dạng `lo ≤ n ≤ hi`; phần giới hạn vẫn cần đối chiếu thủ công.");
        if (result.ParsedBounds > 0 && result.Errors.Count == 0)
            result.Warnings.Add("Đã đối chiếu các cận số nhận diện được. Những biểu thức khác (tích, tổng, điều kiện logic) vẫn cần rà thủ công.");
        return result;
    }
}
