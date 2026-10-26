// GraphProblem.cs - Đồ thị.
//
// Định dạng input:
//   n m
//   m dòng: u v      (hoặc u v w nếu weighted = 1), 1 <= u, v <= n
//
// Tùy chọn:
//   directed  = 1 : có hướng (u -> v)
//   simple    = 1 : đồ thị đơn - không khuyên (u = v), không cạnh lặp
//   connected = 1 : liên thông (có hướng: liên thông yếu), cần m >= n - 1
//   weighted  = 1 : mỗi cạnh có trọng số w trong [minW, maxW]
using System.Text;

namespace OjGen.Core;

public sealed class GraphProblem : Problem
{
    private const long MaxV = 200000;
    private const long MaxE = 1000000;
    private const ulong EnumLimit = 2000000;   // liệt kê toàn bộ cặp đỉnh khi số cặp <= ngưỡng này

    public override string Id => "graph";
    public override string Title => "Đồ thị";
    public override string FormatDescription =>
        "Dòng 1: n m (số đỉnh, số cạnh).\n" +
        "m dòng tiếp: u v (hoặc u v w khi weighted = 1), đỉnh đánh số 1..n.\n" +
        "  directed = 1: có hướng.  simple = 1: không khuyên, không cạnh lặp.\n" +
        "  connected = 1: liên thông (có hướng: liên thông yếu), cần m >= n - 1.\n" +
        "  Đồ thị đơn vô hướng n đỉnh có tối đa n(n-1)/2 cạnh; có hướng tối đa n(n-1).\n" +
        "Ví dụ (vô hướng, có trọng số):\n  4 3\n  1 2 7\n  2 4 3\n  3 1 10\n";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ParamSpec.Int("minN", "Số đỉnh nhỏ nhất (minN)", 2, 1, MaxV),
        ParamSpec.Int("maxN", "Số đỉnh lớn nhất (maxN)", 8, 1, MaxV),
        ParamSpec.Int("minM", "Số cạnh nhỏ nhất (minM)", 1, 0, MaxE),
        ParamSpec.Int("maxM", "Số cạnh lớn nhất (maxM)", 10, 0, MaxE),
        ParamSpec.Bool("directed", "Đồ thị có hướng?", false),
        ParamSpec.Bool("simple", "Đồ thị đơn (không khuyên, không cạnh lặp)?", true),
        ParamSpec.Bool("connected", "Bắt buộc liên thông?", false),
        ParamSpec.Bool("weighted", "Có trọng số cạnh?", false),
        ParamSpec.Int("minW", "Trọng số nhỏ nhất", 1, long.MinValue, long.MaxValue, "chỉ dùng khi có trọng số",
            visibleWhen: c => c.B("weighted")),
        ParamSpec.Int("maxW", "Trọng số lớn nhất", 1000000000, long.MinValue, long.MaxValue,
            visibleWhen: c => c.B("weighted")),
    };

    private sealed class Dsu
    {
        private readonly int[] _p;
        public Dsu(int n)
        {
            _p = new int[n + 1];
            for (int i = 0; i < _p.Length; i++) _p[i] = i;
        }
        public int Find(int x)
        {
            while (_p[x] != x) x = _p[x] = _p[_p[x]];
            return x;
        }
        public bool Unite(int a, int b)
        {
            a = Find(a); b = Find(b);
            if (a == b) return false;
            _p[a] = b;
            return true;
        }
    }

    private static ulong MaxEdgesSimple(long n, bool directed)
    {
        ulong un = (ulong)n;
        return directed ? un * (un - 1) : un * (un - 1) / 2;
    }

    /// <summary>Miền số cạnh hợp lệ với n đỉnh. Trả về false nếu rỗng.</summary>
    private static bool EdgeRange(Config c, long n, out long lo, out long hi)
    {
        lo = c.I("minM");
        if (c.B("connected")) lo = Math.Max(lo, n - 1);
        hi = c.I("maxM");
        if (c.B("simple"))
        {
            ulong me = MaxEdgesSimple(n, c.B("directed"));
            if (me < (ulong)hi) hi = (long)me;
        }
        return lo <= hi;
    }

    public override string? ValidateConfig(Config c)
    {
        if (c.I("minN") > c.I("maxN")) return "minN phải <= maxN.";
        if (c.I("minM") > c.I("maxM")) return "minM phải <= maxM.";
        if (c.B("weighted") && c.I("minW") > c.I("maxW")) return "minW phải <= maxW.";
        for (long n = c.I("minN"); n <= c.I("maxN"); n++)
            if (EdgeRange(c, n, out _, out _)) return null;
        string why = "Không có số đỉnh n nào trong [minN, maxN] cho phép số cạnh trong [minM, maxM].";
        if (c.B("simple"))
            why += "\n  Đồ thị đơn " + (c.B("directed") ? "có hướng" : "vô hướng") + " với maxN = " +
                   Util.LL(c.I("maxN")) + " đỉnh có tối đa " +
                   Util.LL(MaxEdgesSimple(c.I("maxN"), c.B("directed"))) + " cạnh.";
        if (c.B("connected"))
            why += "\n  Đồ thị liên thông với minN = " + Util.LL(c.I("minN")) + " đỉnh cần ít nhất " +
                   Util.LL(c.I("minN") - 1) + " cạnh.";
        return why;
    }

    public override string Generate(Rng rng, Config c)
    {
        bool directed = c.B("directed"), simple = c.B("simple"), connected = c.B("connected");
        // Chọn n sao cho tồn tại số cạnh hợp lệ.
        long n = 0, lo = 0, hi = -1;
        for (int tries = 0; tries < 64; tries++)
        {
            n = rng.Range(c.I("minN"), c.I("maxN"));
            if (EdgeRange(c, n, out lo, out hi)) break;
        }
        if (lo > hi)
        {
            var ok = new List<long>();
            for (long x = c.I("minN"); x <= c.I("maxN"); x++)
                if (EdgeRange(c, x, out lo, out hi)) ok.Add(x);
            n = ok[(int)rng.Below((ulong)ok.Count)];
            EdgeRange(c, n, out lo, out hi);
        }
        long m = rng.Range(lo, hi);
        ulong un = (ulong)n;
        ulong Key(long u, long v)
        {
            if (!directed && u > v) (u, v) = (v, u);
            return (ulong)(u - 1) * un + (ulong)(v - 1);
        }

        var edges = new List<(long U, long V)>((int)m);
        var used = new HashSet<ulong>();
        if (connected && n > 1)
        {
            // Cây khung ngẫu nhiên: mỗi đỉnh nối với một đỉnh đứng trước trong hoán vị.
            var perm = new List<long>((int)n);
            for (long i = 1; i <= n; i++) perm.Add(i);
            rng.Shuffle(perm);
            for (int i = 1; i < perm.Count; i++)
            {
                long u = perm[i], v = perm[(int)rng.Below((ulong)i)];
                if (rng.Chance(50)) (u, v) = (v, u);
                edges.Add((u, v));
                if (simple) used.Add(Key(u, v));
            }
        }
        int need = (int)m - edges.Count;
        if (!simple)
        {
            for (int k = 0; k < need; k++)
            {
                // Thứ tự đánh giá đối số của emplace_back(rng.range, rng.range) trong bản C++ (g++)
                // là từ phải sang trái; giữ nguyên để cùng seed cho cùng file test.
                long v = rng.Range(1, n);
                long u = rng.Range(1, n);
                edges.Add((u, v));
            }
        }
        else if (need > 0)
        {
            ulong maxE = MaxEdgesSimple(n, directed);
            if (maxE <= EnumLimit && (ulong)m * 2 > maxE)
            {
                // Đồ thị dày: liệt kê mọi cặp còn trống rồi chọn ngẫu nhiên.
                var cand = new List<(long U, long V)>((int)maxE);
                for (long u = 1; u <= n; u++)
                    for (long v = directed ? 1 : u + 1; v <= n; v++)
                        if (u != v && !used.Contains(Key(u, v))) cand.Add((u, v));
                for (int k = 0; k < need; k++)
                {
                    int j = k + (int)rng.Below((ulong)(cand.Count - k));
                    (cand[k], cand[j]) = (cand[j], cand[k]);
                    edges.Add(cand[k]);
                }
            }
            else
            {
                while (edges.Count < (int)m)
                {
                    long u = rng.Range(1, n), v = rng.Range(1, n);   // C++: khai báo hai biến, thứ tự trái -> phải
                    if (u == v) continue;
                    if (used.Add(Key(u, v))) edges.Add((u, v));
                }
            }
        }
        rng.Shuffle(edges);
        if (!directed)
            for (int i = 0; i < edges.Count; i++)
                if (rng.Chance(50)) edges[i] = (edges[i].V, edges[i].U);

        var sb = new StringBuilder((int)Math.Min(int.MaxValue / 2, m * 26 + 24));
        Util.AppendLL(sb, n);
        sb.Append(' ');
        Util.AppendLL(sb, m);
        sb.Append('\n');
        foreach (var e in edges)
        {
            Util.AppendLL(sb, e.U);
            sb.Append(' ');
            Util.AppendLL(sb, e.V);
            if (c.B("weighted"))
            {
                sb.Append(' ');
                Util.AppendLL(sb, rng.Range(c.I("minW"), c.I("maxW")));
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (SplitInputLines(input, out var L) is { } e) return e;
        if (L[0].Length != 2) return "Dòng 1 phải chứa n m.";
        if (ReadIntToken(L[0][0], c.I("minN"), c.I("maxN"), "n", out long n) is { } e1) return e1;
        if (!EdgeRange(c, n, out long lo, out long hi)) return $"Không có số cạnh hợp lệ với n = {Util.LL(n)}.";
        if (ReadIntToken(L[0][1], lo, hi, "m", out long m) is { } e2) return e2;
        if (L.Count != m + 1) return "Số dòng cạnh khác m.";
        bool directed = c.B("directed");
        int width = c.B("weighted") ? 3 : 2;
        var seen = new HashSet<ulong>();
        var dsu = new Dsu((int)n);
        long comps = n;
        for (long k = 1; k <= m; k++)
        {
            var t = L[(int)k];
            string where = $"Cạnh {k}";
            if (t.Length != width) return $"{where}: cần {width} số.";
            if (ReadIntToken(t[0], 1, n, where + " (u)", out long u) is { } e3) return e3;
            if (ReadIntToken(t[1], 1, n, where + " (v)", out long v) is { } e4) return e4;
            if (width == 3 && ReadIntToken(t[2], c.I("minW"), c.I("maxW"), where + " (w)", out _) is { } e5) return e5;
            if (c.B("simple"))
            {
                if (u == v) return where + ": khuyên (u = v) trong đồ thị đơn.";
                long a = u, b = v;
                if (!directed && a > b) (a, b) = (b, a);
                if (!seen.Add((ulong)(a - 1) * (ulong)n + (ulong)(b - 1)))
                    return where + ": cạnh lặp trong đồ thị đơn.";
            }
            if (dsu.Unite((int)u, (int)v)) comps--;
        }
        if (c.B("connected") && comps != 1) return "Đồ thị không liên thông.";
        return null;
    }

    public override ulong EstimateMaxBytes(Config c)
    {
        ulong per = 2 * (ulong)Util.LL(c.I("maxN")).Length + 2;
        if (c.B("weighted")) per += Util.MaxDigits(c.I("minW"), c.I("maxW")) + 1;
        return (ulong)c.I("maxM") * per + 24;
    }
}
