namespace OjGen.Core;

public sealed class CoverageReport
{
    public string Summary { get; set; } = "";
    public string Confidence { get; set; } = "Heuristic";
    public List<string> Observations { get; } = new();
    public List<string> Suggestions { get; } = new();

    public string ToText()
    {
        var lines = new List<string> { Summary, "", "Đã quan sát:" };
        lines.AddRange(Observations.Select(item => "• " + item));
        lines.Add("");
        lines.Add("Độ tin cậy: " + Confidence + ".");
        lines.Add("");
        lines.Add("Gợi ý bổ sung:");
        lines.AddRange(Suggestions.Count == 0 ? new[] { "• Chưa thấy thiếu sót rõ ràng từ cấu hình và kích thước test." } : Suggestions.Select(item => "• " + item));
        lines.Add("");
        lines.Add("Các phép đo dùng định dạng input mà OjGen nhận diện; phân loại trường hợp và gợi ý còn mang tính heuristic, không thay thế việc rà soát logic đề.");
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

    private static string? Dimension(string key)
    {
        string k = key.ToLowerInvariant();
        if (k.StartsWith("min", StringComparison.Ordinal)) k = k[3..];
        else if (k.StartsWith("max", StringComparison.Ordinal)) k = k[3..];
        return k switch
        {
            "n" or "len" or "length" or "size" or "count" => "n",
            "m" => "m",
            "q" => "q",
            "t" => "t",
            "h" or "rows" => "rows",
            "w" or "cols" => "cols",
            _ => null,
        };
    }

    private static bool TryNumber(string value, out long number) => Util.ParseLL(value, out number);

    private static Dictionary<string, List<long>> MeasureDimensions(Problem problem, IReadOnlyList<string> inputs,
        IReadOnlyList<TestGroupSpec> groups, IReadOnlyList<ManualTestSpec> manualTests)
    {
        var result = new Dictionary<string, List<long>>(StringComparer.OrdinalIgnoreCase);
        void Add(string key, long value)
        {
            if (!result.TryGetValue(key, out var values)) result[key] = values = new List<long>();
            values.Add(value);
        }
        Config ConfigAt(int index)
        {
            if (index < manualTests.Count) return manualTests[index].Cfg;
            index -= manualTests.Count;
            foreach (var group in groups)
            {
                if (index < group.TestCount) return group.Cfg;
                index -= group.TestCount;
            }
            return problem.DefaultConfig();
        }
        for (int i = 0; i < inputs.Count; i++)
        {
            try
            {
                using var reader = new StreamReader(inputs[i]);
                string? firstLine = reader.ReadLine();
                while (firstLine != null && string.IsNullOrWhiteSpace(firstLine)) firstLine = reader.ReadLine();
                firstLine ??= "";
                string[] first = firstLine.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                Config cfg = ConfigAt(i);
                if (problem.Id == "string")
                {
                    long length;
                    if (cfg.B("printLen") && first.Length == 1 && TryNumber(first[0], out var declared)) length = declared;
                    else
                    {
                        string? stringLine = cfg.B("printLen") ? reader.ReadLine() : firstLine;
                        if (stringLine == null) continue;
                        length = stringLine.Length;
                    }
                    Add("len", length);
                    continue;
                }
                if (first.Length == 0) continue;
                if (TryNumber(first[0], out long a))
                {
                    if (problem.Id == "multi_test")
                    {
                        Add("t", a);
                        for (long test = 0; test < a; test++)
                        {
                            string? nLine = reader.ReadLine();
                            if (nLine == null) break;
                            if (TryNumber(nLine.Trim(), out long subN)) Add("n", subN);
                            reader.ReadLine(); // payload line for this subtest
                        }
                    }
                    else if (problem.Id is "graph" or "grid" or "queries" or "prefix_sum_range" or "prefix_subsequence_range" or "tdc_prefix_sum")
                    {
                        Add("n", a);
                        if (first.Length > 1 && TryNumber(first[1], out long b))
                            Add(problem.Id is "queries" or "prefix_sum_range" or "prefix_subsequence_range" or "tdc_prefix_sum" ? "q" : "m", b);
                    }
                    else if (problem.Id == "matrix")
                    {
                        Add("n", a);
                        if (cfg.I("layout") == 0 && first.Length > 1 && TryNumber(first[1], out long b)) Add("m", b);
                    }
                    else if (problem.Id is "array" or "tree" or "pair_list") Add("n", a);
                    else if (first.Length > 1 && TryNumber(first[1], out long b))
                    {
                        Add("n", a);
                        Add("m", b);
                    }
                }
            }
            catch (Exception) { }
        }
        return result;
    }

    public static CoverageReport Analyze(Problem problem, IReadOnlyList<string> inputs,
                                         IReadOnlyList<TestGroupSpec> groups, IReadOnlyList<ManualTestSpec> manualTests,
                                         IReadOnlyList<string>? requiredCoverageTags = null)
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
        var measurements = MeasureDimensions(problem, inputs, groups, manualTests);
        bool small = HasWord(groupNames, SmallWords);
        bool boundary = HasWord(groupNames, BoundaryWords);
        bool stress = HasWord(groupNames, StressWords);
        bool random = HasWord(groupNames, RandomWords);
        int sampleCount = manualTests.Count(test => test.IsSample);
        var observedTags = groups.SelectMany(group => group.CoverageTags ?? new List<string>())
            .Select(tag => tag.Trim().ToLowerInvariant()).Where(tag => tag.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        report.Summary = $"{inputs.Count} input · {groups.Count} nhóm cấu hình · {manualTests.Count} test nhập tay · {sampleCount} sample";
        if (sizes.Count > 0)
        {
            long min = sizes[0], median = sizes[sizes.Count / 2], max = sizes[^1];
            report.Observations.Add($"Kích thước file input: nhỏ nhất {Util.FmtBytes((ulong)min)}, trung vị {Util.FmtBytes((ulong)median)}, lớn nhất {Util.FmtBytes((ulong)max)}.");
        }
        foreach (var measurement in measurements.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (measurement.Value.Count == 0) continue;
            report.Observations.Add($"Giá trị {measurement.Key} đo từ input: {Util.LL(measurement.Value.Min())}–{Util.LL(measurement.Value.Max())} ({measurement.Value.Count} giá trị).");
        }
        if (observedTags.Count > 0)
            report.Observations.Add("Nhãn độ phủ đã khai báo: " + string.Join(", ", observedTags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)) + ".");
        report.Confidence = measurements.Count > 0
            ? "Có đo kích thước thực tế ở các định dạng nhận diện được; các nhận định còn lại là heuristic"
            : "Heuristic (không nhận diện được trường kích thước của input)";
        if (observedTags.Count > 0) report.Confidence += "; nhãn do người dùng khai báo";
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

        foreach (string requiredTag in (requiredCoverageTags ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (observedTags.Contains(requiredTag)) continue;
            if (report.Suggestions.Count < 8)
                report.Suggestions.Add($"Thiếu nhóm test có nhãn độ phủ bắt buộc '{requiredTag}'.");
        }

        var configs = groups.Select(group => group.Cfg).Concat(manualTests.Select(test => test.Cfg)).ToList();
        foreach (var lowParam in problem.Params.Where(param => param.Type == ParamType.Int && param.Key.StartsWith("min", StringComparison.OrdinalIgnoreCase) && IsSizeParameter(param.Key)))
        {
            var highParam = problem.Params.FirstOrDefault(param => param.Type == ParamType.Int && param.Key.Equals("max" + lowParam.Key[3..], StringComparison.OrdinalIgnoreCase));
            string? dimension = Dimension(lowParam.Key);
            if (highParam == null || dimension == null || !measurements.TryGetValue(dimension, out var observed) || observed.Count == 0 || configs.Count == 0) continue;
            long allowedLow = configs.Min(config => config.I(lowParam.Key));
            long allowedHigh = configs.Max(config => config.I(highParam.Key));
            long actualLow = observed.Min(), actualHigh = observed.Max();
            if (actualLow > allowedLow && report.Suggestions.Count < 8)
                report.Suggestions.Add($"Thêm test cố định ở biên thấp {lowParam.DisplayLabel}: cấu hình cho phép {Util.LL(allowedLow)}, nhưng input nhỏ nhất đo được là {Util.LL(actualLow)}.");
            if (actualHigh < allowedHigh && report.Suggestions.Count < 8)
                report.Suggestions.Add($"Thêm test cố định ở biên cao {highParam.DisplayLabel}: cấu hình cho phép {Util.LL(allowedHigh)}, nhưng input lớn nhất đo được là {Util.LL(actualHigh)}.");
        }

        if (!small && report.Suggestions.Count < 8) report.Suggestions.Add("Thêm nhóm 'Nhỏ' để kiểm tra input tối thiểu và các trường hợp ít phần tử.");
        if (!boundary && report.Suggestions.Count < 8) report.Suggestions.Add("Thêm nhóm 'Biên' cho giá trị sát giới hạn, rỗng/gần rỗng hoặc cấu trúc đặc biệt.");
        if (!random && report.Suggestions.Count < 8) report.Suggestions.Add("Thêm nhóm 'Ngẫu nhiên' với seed cố định để dễ tái lập.");
        if (!stress && report.Suggestions.Count < 8) report.Suggestions.Add("Thêm nhóm 'Stress' gần giới hạn lớn nhất để kiểm tra thời gian và bộ nhớ.");

        if (report.Observations.Count == 0) report.Observations.Add("Chưa có metadata cấu hình hoặc kích thước input để phân loại thêm.");
        return report;
    }
}
