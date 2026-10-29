// TreeProblem.cs - Cây dạng danh sách cạnh hoặc danh sách cha.
using System.Text;

namespace OjGen.Core;

public sealed class TreeProblem : Problem
{
    private const long MaxN = 200000;

    public override string Id => "tree";
    public override string Title => "Cây";
    public override string MainCategory => "Đồ thị và cây";
    public override string FormatDescription =>
        "Dòng 1: n. Có thể ghi n-1 cạnh u v [w], hoặc ghi n-1 cha của các đỉnh 2..n trên một dòng.\n" +
        "Cấu hình chọn kiểu cây: ngẫu nhiên, đường thẳng, hình sao hoặc cây nhị phân.\n" +
        "Danh sách cạnh có thể thêm trọng số.";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ParamSpec.Int("layout", "Cách ghi cây", 0, 0, 1,
            options: new[] { new ParamOption(0, "Danh sách cạnh u v"), new ParamOption(1, "Danh sách cha của 2..n") }),
        ParamSpec.Int("minN", "Số đỉnh nhỏ nhất", 1, 1, MaxN),
        ParamSpec.Int("maxN", "Số đỉnh lớn nhất", 20, 1, MaxN),
        ParamSpec.Int("shape", "Cấu trúc cây", 0, 0, 3,
            options: new[]
            {
                new ParamOption(0, "Cây ngẫu nhiên"),
                new ParamOption(1, "Đường thẳng"),
                new ParamOption(2, "Hình sao"),
                new ParamOption(3, "Cây nhị phân"),
            }),
        ParamSpec.Bool("weighted", "Cạnh có trọng số?", false,
            visibleWhen: c => c.I("layout") == 0),
        ParamSpec.Int("minW", "Trọng số nhỏ nhất", 1, long.MinValue, long.MaxValue,
            visibleWhen: c => c.I("layout") == 0 && c.B("weighted")),
        ParamSpec.Int("maxW", "Trọng số lớn nhất", 100, long.MinValue, long.MaxValue,
            visibleWhen: c => c.I("layout") == 0 && c.B("weighted")),
    };

    public override string? ValidateConfig(Config c)
    {
        if (c.I("minN") > c.I("maxN")) return "minN phải <= maxN.";
        if (c.I("layout") == 0 && c.B("weighted") && c.I("minW") > c.I("maxW"))
            return "minW phải <= maxW.";
        return null;
    }

    public override string Generate(Rng rng, Config c)
    {
        int n = (int)rng.Range(c.I("minN"), c.I("maxN"));
        var parent = new int[n + 1];
        for (int v = 2; v <= n; v++)
        {
            parent[v] = c.I("shape") switch
            {
                1 => v - 1,
                2 => 1,
                3 => v / 2,
                _ => (int)rng.Range(1, v - 1),
            };
        }

        var sb = new StringBuilder();
        Util.AppendLL(sb, n); sb.Append('\n');
        if (c.I("layout") == 1)
        {
            if (n > 1)
            {
                for (int v = 2; v <= n; v++)
                {
                    if (v > 2) sb.Append(' ');
                    Util.AppendLL(sb, parent[v]);
                }
                sb.Append('\n');
            }
        }
        else
        {
            for (int v = 2; v <= n; v++)
            {
                Util.AppendLL(sb, parent[v]); sb.Append(' '); Util.AppendLL(sb, v);
                if (c.B("weighted"))
                {
                    sb.Append(' ');
                    Util.AppendLL(sb, rng.Range(c.I("minW"), c.I("maxW")));
                }
                sb.Append('\n');
            }
        }
        return sb.ToString();
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (SplitInputLines(input, out var lines) is { } e) return e;
        if (lines[0].Length != 1) return "Dòng 1 phải chứa n.";
        if (ReadIntToken(lines[0][0], c.I("minN"), c.I("maxN"), "n", out long n) is { } e1) return e1;
        int layout = (int)c.I("layout");
        if (layout == 1)
        {
            int expected = n == 1 ? 1 : 2;
            if (lines.Count != expected) return $"Danh sách cha cần {expected} dòng.";
            if (n == 1) return null;
            var parents = lines[1];
            if (parents.Length != n - 1) return $"Dòng 2 phải chứa đúng {Util.LL(n - 1)} số cha.";
            for (int child = 2; child <= n; child++)
                if (ReadIntToken(parents[child - 2], 1, child - 1, $"cha của đỉnh {child}", out _) is { } e2) return e2;
            return null;
        }

        if (lines.Count != n) return $"Cây n đỉnh phải có đúng {Util.LL(n - 1)} dòng cạnh.";
        bool weighted = c.B("weighted");
        int width = weighted ? 3 : 2;
        int[] dsu = new int[(int)n + 1];
        for (int i = 0; i < dsu.Length; i++) dsu[i] = i;
        int Find(int x)
        {
            while (dsu[x] != x) { dsu[x] = dsu[dsu[x]]; x = dsu[x]; }
            return x;
        }
        var seen = new HashSet<ulong>();
        for (int edge = 1; edge < n; edge++)
        {
            var row = lines[edge];
            if (row.Length != width) return $"Cạnh {edge} phải có {width} số.";
            if (ReadIntToken(row[0], 1, n, $"Cạnh {edge} (u)", out long u) is { } e3) return e3;
            if (ReadIntToken(row[1], 1, n, $"Cạnh {edge} (v)", out long v) is { } e4) return e4;
            if (u == v) return $"Cạnh {edge} không được là khuyên.";
            if (weighted && ReadIntToken(row[2], c.I("minW"), c.I("maxW"), $"Cạnh {edge} (w)", out _) is { } e5) return e5;
            long a = Math.Min(u, v), b = Math.Max(u, v);
            if (!seen.Add((ulong)(a - 1) * (ulong)n + (ulong)(b - 1))) return $"Cạnh {edge} bị lặp.";
            int ru = Find((int)u), rv = Find((int)v);
            if (ru == rv) return $"Cạnh {edge} tạo chu trình.";
            dsu[ru] = rv;
        }
        return null;
    }

    public override ulong EstimateMaxBytes(Config c)
    {
        ulong perEdge = 2 * (ulong)Util.LL(c.I("maxN")).Length + 3;
        if (c.I("layout") == 0 && c.B("weighted")) perEdge += Util.MaxDigits(c.I("minW"), c.I("maxW")) + 1;
        return (ulong)c.I("maxN") * perEdge + 32;
    }
}
