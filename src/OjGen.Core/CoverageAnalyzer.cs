namespace OjGen.Core;

public sealed class CoverageReport
{
    public string Summary { get; set; } = "";
    public List<string> Observations { get; } = new();
    public List<string> Suggestions { get; } = new();

    public string ToText()
    {
        var lines = new List<string> { Summary, "", "Đã quan sát:" };
        lines.AddRange(Observations.Select(item => "• " + item));
        lines.Add("");
        lines.Add("Gợi ý bổ sung:");
        lines.AddRange(Suggestions.Count == 0 ? new[] { "• Chưa thấy thiếu sót rõ ràng từ cấu hình và kích thước test." } : Suggestions.Select(item => "• " + item));
        lines.Add("");
        lines.Add("Đây là gợi ý heuristic dựa trên cấu hình khuôn, tên nhóm và kích thước input; không thay thế việc rà soát logic đề.");
        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>Tóm tắt cấu hình và kích thước thật của bộ test, sau đó gợi ý các chế độ còn thiếu.</summary>
public static class CoverageAnalyzer
{
    private static readonly string[] SmallWords = { "small", "mini", "nhỏ", "min" };
    private static readonly string[] BoundaryWords = { "edge", "boundary", "biên", "corner" };
    private static readonly string[] StressWords = { "stress", "large", "max", "lớn", "khổng lồ" };
    private static readonly string[] RandomWords = { "random", "ngẫu", "rand" };

    private static bool HasWord(IEnumerable<string> names, IEnumerable<string> words) =>
        names.Any(name => words.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)));

    private static bool IsSizeParameter(string key)
    {
        string k = key.ToLowerInvariant();
        return k is "n" or "minn" or "maxn" or "m" or "minm" or "maxm" or "q" or "minq" or "maxq" or
            "t" or "mint" or "maxt" or "len" or "minlen" or "maxlen" or "length" or "minlength" or
            "maxlength" or "h" or "w" or "rows" or "cols" or "size" or "count";
    }

    public static CoverageReport Analyze(Problem problem, IReadOnlyList<string> inputs,
                                         IReadOnlyList<TestGroupSpec> groups, IReadOnlyList<ManualTestSpec> manualTests)
    {
        var report = new CoverageReport();
        var sizes = new List<long>(inputs.Count);
        foreach (var input in inputs)
        {
            try { sizes.Add(new FileInfo(input).Length); }
            catch (Exception) { }
        }
        sizes.Sort();
        var groupNames = groups.Select(group => group.Name).ToList();
        bool small = HasWord(groupNames, SmallWords);
        bool boundary = HasWord(groupNames, BoundaryWords);
        bool stress = HasWord(groupNames, StressWords);
        bool random = HasWord(groupNames, RandomWords);
        int sampleCount = manualTests.Count(test => test.IsSample);

        report.Summary = $"{inputs.Count} input · {groups.Count} nhóm cấu hình · {manualTests.Count} test nhập tay · {sampleCount} sample";
        if (sizes.Count > 0)
        {
            long min = sizes[0], median = sizes[sizes.Count / 2], max = sizes[^1];
            report.Observations.Add($"Kích thước file input: nhỏ nhất {Util.FmtBytes((ulong)min)}, trung vị {Util.FmtBytes((ulong)median)}, lớn nhất {Util.FmtBytes((ulong)max)}.");
        }
        if (small) report.Observations.Add("Có nhóm được đặt tên theo test nhỏ.");
        if (boundary) report.Observations.Add("Có nhóm được đặt tên theo trường hợp biên.");
        if (random) report.Observations.Add("Có nhóm ngẫu nhiên.");
        if (stress) report.Observations.Add("Có nhóm stress/kích thước lớn.");
        if (sampleCount > 0) report.Observations.Add($"Có {sampleCount} test mẫu nhập tay.");

        foreach (var param in problem.Params.Where(param => param.Options is { Count: > 1 }))
        {
            var options = param.Options!;
            var visibleConfigs = groups.Select(group => group.Cfg).Concat(manualTests.Select(test => test.Cfg))
                .Where(config => param.VisibleWhen == null || param.VisibleWhen(config)).ToList();
            if (visibleConfigs.Count == 0) continue;
            var used = visibleConfigs.Select(config => config.I(param.Key)).ToHashSet();
            var present = options.Where(option => used.Contains(option.Value)).ToList();
            var missing = options.Where(option => !used.Contains(option.Value)).ToList();
            if (present.Count > 0)
                report.Observations.Add($"{param.DisplayLabel}: {string.Join(", ", present.Select(option => option.Label))}.");
            if (missing.Count > 0 && report.Suggestions.Count < 8)
                report.Suggestions.Add($"Thêm nhóm cho '{param.DisplayLabel}': {string.Join(", ", missing.Select(option => option.Label))}.");
        }

        foreach (var param in problem.Params.Where(param => param.Type == ParamType.Int && param.Options == null && IsSizeParameter(param.Key) && param.Hi > param.Lo))
        {
            var values = groups.Select(group => group.Cfg.I(param.Key)).Concat(manualTests.Select(test => test.Cfg.I(param.Key))).ToList();
            if (values.Count == 0) continue;
            if (values.Min() > param.Lo && report.Suggestions.Count < 8)
                report.Suggestions.Add($"Thử '{param.DisplayLabel}' ở biên thấp {Util.LL(param.Lo)} (hiện nhỏ nhất {Util.LL(values.Min())}).");
            if (values.Max() < param.Hi && report.Suggestions.Count < 8)
                report.Suggestions.Add($"Thử '{param.DisplayLabel}' ở biên cao {Util.LL(param.Hi)} (hiện lớn nhất {Util.LL(values.Max())}).");
        }

        if (!small && report.Suggestions.Count < 8) report.Suggestions.Add("Thêm nhóm 'Nhỏ' để kiểm tra input tối thiểu và các trường hợp ít phần tử.");
        if (!boundary && report.Suggestions.Count < 8) report.Suggestions.Add("Thêm nhóm 'Biên' cho giá trị sát giới hạn, rỗng/gần rỗng hoặc cấu trúc đặc biệt.");
        if (!random && report.Suggestions.Count < 8) report.Suggestions.Add("Thêm nhóm 'Ngẫu nhiên' với seed cố định để dễ tái lập.");
        if (!stress && report.Suggestions.Count < 8) report.Suggestions.Add("Thêm nhóm 'Stress' gần giới hạn lớn nhất để kiểm tra thời gian và bộ nhớ.");

        if (report.Observations.Count == 0) report.Observations.Add("Chưa có metadata cấu hình hoặc kích thước input để phân loại thêm.");
        return report;
    }
}
