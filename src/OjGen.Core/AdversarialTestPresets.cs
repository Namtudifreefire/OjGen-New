namespace OjGen.Core;

/// <summary>Creates editable edge-case groups from the user's current configuration.</summary>
public static class AdversarialTestPresets
{
    private static readonly (string Min, string Max)[] Dimensions =
    {
        ("minN", "maxN"), ("minLen", "maxLen"), ("minRows", "maxRows"),
        ("minCols", "maxCols"), ("minQ", "maxQ"), ("minT", "maxT"),
    };

    public static IReadOnlyList<TestGroupSpec> Build(Problem problem, Config source, IEnumerable<string> existingNames)
    {
        var used = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var result = new List<TestGroupSpec>();
        void Add(string name, Config config)
        {
            if (Generator.ValidateFullConfig(problem, config) is not null) return;
            string unique = name;
            for (int n = 2; used.Contains(unique); n++) unique = name + " " + n;
            used.Add(unique);
            result.Add(new TestGroupSpec { Name = unique, TestCount = 5, Cfg = config, CoverageTags = new List<string> { "adversarial" } });
        }

        var low = source.Clone();
        bool changed = false;
        foreach (var (minKey, maxKey) in Dimensions)
        {
            if (!problem.Params.Any(p => p.Key.Equals(minKey, StringComparison.OrdinalIgnoreCase)) ||
                !problem.Params.Any(p => p.Key.Equals(maxKey, StringComparison.OrdinalIgnoreCase))) continue;
            long value = source.I(minKey);
            low.Set(minKey, value); low.Set(maxKey, value); changed = true;
        }
        if (changed) Add("Biên nhỏ", low);

        var upper = source.Clone();
        changed = false;
        foreach (var (minKey, maxKey) in Dimensions)
        {
            if (!problem.Params.Any(p => p.Key.Equals(minKey, StringComparison.OrdinalIgnoreCase)) ||
                !problem.Params.Any(p => p.Key.Equals(maxKey, StringComparison.OrdinalIgnoreCase))) continue;
            long value = source.I(maxKey);
            upper.Set(minKey, value); upper.Set(maxKey, value); changed = true;
        }
        if (changed) Add("Biên lớn", upper);

        if (problem.Id == "array" && source.I("mode") == 0)
        {
            var repeated = source.Clone();
            repeated.Set("distinct", 0);
            repeated.Set("minVal", 0);
            repeated.Set("maxVal", 0);
            Add("Nhiều phần tử trùng", repeated);
        }

        if (problem.Id == "graph" && source.B("directed"))
        {
            var strong = source.Clone();
            strong.Set("stronglyConnected", 1);
            strong.Set("dag", 0);
            strong.Set("bipartite", 0);
            long vertices = Math.Max(2, source.I("minN"));
            strong.Set("minN", vertices); strong.Set("maxN", vertices);
            long minEdges = source.B("simple") ? vertices : vertices;
            strong.Set("minM", Math.Max(source.I("minM"), minEdges));
            strong.Set("maxM", Math.Max(strong.I("maxM"), strong.I("minM")));
            Add("Đồ thị liên thông mạnh", strong);
        }
        return result;
    }
}
