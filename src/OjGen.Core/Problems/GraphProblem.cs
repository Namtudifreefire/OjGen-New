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
//   stronglyConnected = 1 : đồ thị có hướng liên thông mạnh, cần m >= n
//   dag       = 1 : đồ thị có hướng không chu trình
//   bipartite = 1 : mỗi cạnh nối hai phía khác nhau
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
    public override string MainCategory => "Đồ thị và cây";
    public override string FormatDescription =>
        "Dòng 1: n m (số đỉnh, số cạnh).\n" +
        "m dòng tiếp: u v (hoặc u v w khi weighted = 1), đỉnh đánh số 1..n.\n" +
        "  directed = 1: có hướng.  simple = 1: không khuyên, không cạnh lặp.\n" +
        "  connected = 1: liên thông (có hướng: liên thông yếu), cần m >= n - 1.\n" +
        "  stronglyConnected = 1: đồ thị có hướng liên thông mạnh, cần m >= n; không kết hợp DAG/hai phía.\n" +
        "  dag = 1: có hướng không chu trình.  bipartite = 1: cạnh chỉ nối hai phía.\n" +
        "  density = 1/2/3: thưa/vừa/dày, tự chọn m theo sức chứa hợp lệ; chỉ dùng với đồ thị đơn.\n" +
        "  Đồ thị đơn vô hướng n đỉnh có tối đa n(n-1)/2 cạnh; có hướng tối đa n(n-1).\n" +
        "Ví dụ (vô hướng, có trọng số):\n  4 3\n  1 2 7\n  2 4 3\n  3 1 10\n";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ParamSpec.Int("minN", "Số đỉnh nhỏ nhất (minN)", 2, 1, MaxV),
        ParamSpec.Int("maxN", "Số đỉnh lớn nhất (maxN)", 8, 1, MaxV),
        ParamSpec.Int("density", "Mật độ cạnh", 0, 0, 3,
            "Tự chọn khoảng số cạnh theo n và các ràng buộc cấu trúc; chỉ dùng với đồ thị đơn.",
            options: new[]
            {
                new ParamOption(0, "Tự đặt minM/maxM"),
                new ParamOption(1, "Thưa"),
                new ParamOption(2, "Vừa"),
                new ParamOption(3, "Dày"),
            }),
        ParamSpec.Int("minM", "Số cạnh nhỏ nhất (minM)", 1, 0, MaxE, "Chỉ dùng khi mật độ = tự đặt.",
            visibleWhen: c => c.I("density") == 0),
        ParamSpec.Int("maxM", "Số cạnh lớn nhất (maxM)", 10, 0, MaxE, "Chỉ dùng khi mật độ = tự đặt.",
            visibleWhen: c => c.I("density") == 0),
        ParamSpec.Bool("directed", "Đồ thị có hướng?", false),
        ParamSpec.Bool("simple", "Đồ thị đơn (không khuyên, không cạnh lặp)?", true),
        ParamSpec.Bool("connected", "Bắt buộc liên thông?", false),
        ParamSpec.Bool("stronglyConnected", "Đồ thị có hướng liên thông mạnh?", false, visibleWhen: c => c.B("directed")),
        ParamSpec.Bool("dag", "Đồ thị có hướng không chu trình (DAG)?", false, visibleWhen: c => c.B("directed")),
        ParamSpec.Bool("bipartite", "Đồ thị hai phía?", false),
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

    private static ulong MaxEdgesAllowed(Config c, long n)
    {
        ulong un = (ulong)n;
        if (!c.B("bipartite"))
            return c.B("dag") ? un * (un - 1) / 2 : MaxEdgesSimple(n, c.B("directed"));
        ulong left = un / 2, right = un - left;
        ulong cross = left * right;
        return c.B("directed") && !c.B("dag") ? cross * 2 : cross;
    }

    private static long PercentCeiling(ulong value, ulong percent) =>
        checked((long)((value * percent + 99) / 100));

    private static (long Low, long High) DensityEdges(Config c, long n)
    {
        ulong capacity = MaxEdgesAllowed(c, n);
        long max = checked((long)capacity);
        long required = c.B("stronglyConnected") ? n : c.B("connected") ? Math.Max(0, n - 1) : 0;
        long low, high;
        switch (c.I("density"))
        {
            case 1: // Thưa: tối đa khoảng ba cạnh mỗi đỉnh.
                low = required;
                high = Math.Min(max, Math.Max(required, checked(n * 3)));
                break;
            case 2: // Vừa: khoảng 20–50% sức chứa.
                low = Math.Max(required, PercentCeiling(capacity, 20));
                high = Math.Min(max, Math.Max(low, PercentCeiling(capacity, 50)));
                break;
            case 3: // Dày: từ 75% đến sức chứa tối đa.
                low = Math.Max(required, PercentCeiling(capacity, 75));
                high = max;
                break;
            default:
                return (c.I("minM"), c.I("maxM"));
        }
        return (low, high);
    }

    /// <summary>Miền số cạnh hợp lệ với n đỉnh. Trả về false nếu rỗng.</summary>
    private static bool EdgeRange(Config c, long n, out long lo, out long hi)
    {
        (lo, hi) = c.I("density") == 0 ? (c.I("minM"), c.I("maxM")) : DensityEdges(c, n);
        if (c.B("connected")) lo = Math.Max(lo, n - 1);
        if (c.B("stronglyConnected")) lo = Math.Max(lo, n);
        if (c.B("simple"))
        {
            ulong me = MaxEdgesAllowed(c, n);
            if (me < (ulong)hi) hi = (long)me;
        }
        else if ((c.B("dag") || c.B("bipartite")) && MaxEdgesAllowed(c, n) == 0)
            hi = 0;
        return lo <= hi;
    }

    /// <summary>Effective m bounds for a group's n range, including the automatic density mode.</summary>
    public bool TryGetEffectiveEdgeBounds(Config c, out long minEdges, out long maxEdges)
    {
        minEdges = long.MaxValue;
        maxEdges = long.MinValue;
        for (long n = c.I("minN"); n <= c.I("maxN"); n++)
        {
            if (!EdgeRange(c, n, out long low, out long high)) continue;
            minEdges = Math.Min(minEdges, low);
            maxEdges = Math.Max(maxEdges, high);
        }
        return maxEdges >= minEdges;
    }

    public override string? ValidateConfig(Config c)
    {
        if (c.I("minN") > c.I("maxN")) return "minN phải <= maxN.";
        if (c.I("density") == 0 && c.I("minM") > c.I("maxM")) return "minM phải <= maxM.";
        if (c.I("density") != 0 && !c.B("simple")) return "Chế độ mật độ cần bật đồ thị đơn (simple = 1).";
        if (c.B("weighted") && c.I("minW") > c.I("maxW")) return "minW phải <= maxW.";
        if (c.B("dag") && !c.B("directed")) return "DAG cần bật directed = 1.";
        if (c.B("stronglyConnected") && !c.B("directed")) return "Liên thông mạnh cần bật directed = 1.";
        if (c.B("stronglyConnected") && c.B("dag")) return "Đồ thị liên thông mạnh không thể đồng thời là DAG (trừ self-loop, hiện không hỗ trợ).";
        if (c.B("stronglyConnected") && c.B("bipartite")) return "Kết hợp stronglyConnected + bipartite hiện chưa được hỗ trợ; hãy tắt một ràng buộc.";
        for (long n = c.I("minN"); n <= c.I("maxN"); n++)
            if (EdgeRange(c, n, out _, out _)) return null;
        string why = c.I("density") == 0
            ? "Không có số đỉnh n nào trong [minN, maxN] cho phép số cạnh trong [minM, maxM]."
            : "Không có số đỉnh n nào thỏa chế độ mật độ cạnh và các ràng buộc đã chọn.";
        if (c.B("simple"))
            why += "\n  Đồ thị đơn " + (c.B("directed") ? "có hướng" : "vô hướng") + " với maxN = " +
                   Util.LL(c.I("maxN")) + " đỉnh có tối đa " +
                   Util.LL(MaxEdgesAllowed(c, c.I("maxN"))) + " cạnh theo ràng buộc đã chọn.";
        if (c.B("connected"))
            why += "\n  Đồ thị liên thông với minN = " + Util.LL(c.I("minN")) + " đỉnh cần ít nhất " +
                   Util.LL(c.I("minN") - 1) + " cạnh.";
        return why;
    }

    public override string Generate(Rng rng, Config c)
    {
        if (c.B("dag") || c.B("bipartite") || c.B("stronglyConnected")) return GenerateConstrained(rng, c);
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

    private string GenerateConstrained(Rng rng, Config c)
    {
        bool directed = c.B("directed"), simple = c.B("simple"), connected = c.B("connected");
        bool dag = c.B("dag"), bipartite = c.B("bipartite"), stronglyConnected = c.B("stronglyConnected");
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
                if (EdgeRange(c, x, out _, out _)) ok.Add(x);
            n = ok[(int)rng.Below((ulong)ok.Count)];
            EdgeRange(c, n, out lo, out hi);
        }
        int vertexCount = checked((int)n);
        long m = rng.Range(lo, hi);
        ulong un = (ulong)n;
        var side = new bool[vertexCount + 1];
        int leftCount = vertexCount / 2;
        if (bipartite)
            for (int v = 1; v <= vertexCount; v++) side[v] = v > leftCount;

        var order = new List<int>(vertexCount);
        if (bipartite)
        {
            var left = Enumerable.Range(1, leftCount).ToList();
            var right = Enumerable.Range(leftCount + 1, vertexCount - leftCount).ToList();
            rng.Shuffle(left); rng.Shuffle(right);
            bool takeLeft = left.Count > 0 && (right.Count == 0 || rng.Chance(50));
            int li = 0, ri = 0;
            while (li < left.Count || ri < right.Count)
            {
                if (takeLeft && li < left.Count) order.Add(left[li++]);
                else if (!takeLeft && ri < right.Count) order.Add(right[ri++]);
                else if (li < left.Count) order.Add(left[li++]);
                else order.Add(right[ri++]);
                takeLeft = !takeLeft;
            }
        }
        else
        {
            order.AddRange(Enumerable.Range(1, vertexCount));
            rng.Shuffle(order);
        }
        var rank = new int[vertexCount + 1];
        for (int i = 0; i < order.Count; i++) rank[order[i]] = i;

        ulong Key(long u, long v)
        {
            if (!directed && u > v) (u, v) = (v, u);
            return (ulong)(u - 1) * un + (ulong)(v - 1);
        }
        var edges = new List<(long U, long V)>((int)m);
        var used = new HashSet<ulong>();
        bool TryAdd(long u, long v)
        {
            if (u == v) return false;
            if (bipartite && side[u] == side[v]) return false;
            if (dag && rank[u] > rank[v]) (u, v) = (v, u);
            if (simple && !used.Add(Key(u, v))) return false;
            edges.Add((u, v));
            return true;
        }

        if (stronglyConnected)
        {
            for (int i = 0; i < order.Count; i++)
                if (!TryAdd(order[i], order[(i + 1) % order.Count]))
                    throw new InvalidOperationException("Không thể tạo chu trình bảo đảm liên thông mạnh.");
        }

        if (connected && !stronglyConnected && vertexCount > 1)
        {
            var priorLeft = new List<int>();
            var priorRight = new List<int>();
            if (bipartite)
            {
                if (side[order[0]]) priorRight.Add(0);
                else priorLeft.Add(0);
            }
            for (int i = 1; i < order.Count; i++)
            {
                int parentIndex;
                if (bipartite)
                {
                    List<int> candidates = side[order[i]] ? priorLeft : priorRight;
                    parentIndex = candidates[(int)rng.Below((ulong)candidates.Count)];
                }
                else parentIndex = (int)rng.Below((ulong)i);
                long u = order[i], v = order[parentIndex];
                if (!dag && directed && rng.Chance(50)) (u, v) = (v, u);
                if (!TryAdd(u, v)) throw new InvalidOperationException("Không thể tạo cây khung thỏa ràng buộc đồ thị.");
                if (bipartite)
                {
                    if (side[order[i]]) priorRight.Add(i);
                    else priorLeft.Add(i);
                }
            }
        }

        int need = (int)m - edges.Count;
        if (!simple)
        {
            while (need-- > 0)
            {
                long u, v;
                if (bipartite)
                {
                    u = rng.Range(1, leftCount);
                    v = rng.Range(leftCount + 1, vertexCount);
                    if (!dag && directed && rng.Chance(50)) (u, v) = (v, u);
                }
                else if (dag)
                {
                    int i = (int)rng.Below((ulong)(vertexCount - 1));
                    int j = i + 1 + (int)rng.Below((ulong)(vertexCount - i - 1));
                    u = order[i]; v = order[j];
                }
                else
                {
                    u = rng.Range(1, vertexCount); v = rng.Range(1, vertexCount);
                    if (u == v) { need++; continue; }
                }
                if (!TryAdd(u, v)) { need++; continue; }
            }
        }
        else if (need > 0)
        {
            ulong maxEdges = MaxEdgesAllowed(c, n);
            if (maxEdges <= EnumLimit && (ulong)m * 2 > maxEdges)
            {
                var candidates = new List<(long U, long V)>((int)maxEdges);
                if (dag)
                {
                    for (int i = 0; i < order.Count; i++)
                        for (int j = i + 1; j < order.Count; j++)
                            if ((!bipartite || side[order[i]] != side[order[j]]) && !used.Contains(Key(order[i], order[j])))
                                candidates.Add((order[i], order[j]));
                }
                else if (directed)
                {
                    for (long u = 1; u <= n; u++)
                        for (long v = 1; v <= n; v++)
                            if (u != v && (!bipartite || side[u] != side[v]) && !used.Contains(Key(u, v)))
                                candidates.Add((u, v));
                }
                else
                {
                    for (long u = 1; u <= n; u++)
                        for (long v = u + 1; v <= n; v++)
                            if ((!bipartite || side[u] != side[v]) && !used.Contains(Key(u, v)))
                                candidates.Add((u, v));
                }
                for (int k = 0; k < need; k++)
                {
                    int j = k + (int)rng.Below((ulong)(candidates.Count - k));
                    (candidates[k], candidates[j]) = (candidates[j], candidates[k]);
                    if (!TryAdd(candidates[k].U, candidates[k].V)) throw new InvalidOperationException("Lỗi chọn cạnh hợp lệ.");
                }
            }
            else
            {
                while (edges.Count < (int)m)
                {
                    long u, v;
                    if (bipartite)
                    {
                        u = rng.Range(1, leftCount);
                        v = rng.Range(leftCount + 1, vertexCount);
                        if (!dag && directed && rng.Chance(50)) (u, v) = (v, u);
                    }
                    else if (dag)
                    {
                        int i = (int)rng.Below((ulong)(vertexCount - 1));
                        int j = i + 1 + (int)rng.Below((ulong)(vertexCount - i - 1));
                        u = order[i]; v = order[j];
                    }
                    else
                    {
                        u = rng.Range(1, vertexCount); v = rng.Range(1, vertexCount);
                        if (u == v) continue;
                    }
                    TryAdd(u, v);
                }
            }
        }

        rng.Shuffle(edges);
        if (!directed)
            for (int i = 0; i < edges.Count; i++)
                if (rng.Chance(50)) edges[i] = (edges[i].V, edges[i].U);
        var sb = new StringBuilder((int)Math.Min(int.MaxValue / 2, m * 26 + 24));
        Util.AppendLL(sb, n); sb.Append(' '); Util.AppendLL(sb, m); sb.Append('\n');
        foreach (var edge in edges)
        {
            Util.AppendLL(sb, edge.U); sb.Append(' '); Util.AppendLL(sb, edge.V);
            if (c.B("weighted")) { sb.Append(' '); Util.AppendLL(sb, rng.Range(c.I("minW"), c.I("maxW"))); }
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
        bool dag = c.B("dag"), bipartite = c.B("bipartite");
        int width = c.B("weighted") ? 3 : 2;
        var seen = new HashSet<ulong>();
        var dsu = new Dsu((int)n);
        var colorAdj = bipartite ? new List<int>[(int)n + 1] : null;
        var dagAdj = dag ? new List<int>[(int)n + 1] : null;
        var indegree = dag ? new int[(int)n + 1] : null;
        var strongAdj = c.B("stronglyConnected") ? new List<int>[(int)n + 1] : null;
        var reverseStrongAdj = c.B("stronglyConnected") ? new List<int>[(int)n + 1] : null;
        long comps = n;
        for (long k = 1; k <= m; k++)
        {
            var t = L[(int)k];
            string where = $"Cạnh {k}";
            if (t.Length != width) return $"{where}: cần {width} số.";
            if (ReadIntToken(t[0], 1, n, where + " (u)", out long u) is { } e3) return e3;
            if (ReadIntToken(t[1], 1, n, where + " (v)", out long v) is { } e4) return e4;
            if (width == 3 && ReadIntToken(t[2], c.I("minW"), c.I("maxW"), where + " (w)", out _) is { } e5) return e5;
            if (dag && u == v) return where + ": khuyên tạo chu trình trong DAG.";
            if (c.B("simple"))
            {
                if (u == v) return where + ": khuyên (u = v) trong đồ thị đơn.";
                long a = u, b = v;
                if (!directed && a > b) (a, b) = (b, a);
                if (!seen.Add((ulong)(a - 1) * (ulong)n + (ulong)(b - 1)))
                    return where + ": cạnh lặp trong đồ thị đơn.";
            }
            if (dsu.Unite((int)u, (int)v)) comps--;
            if (bipartite)
            {
                (colorAdj![(int)u] ??= new List<int>()).Add((int)v);
                (colorAdj![(int)v] ??= new List<int>()).Add((int)u);
            }
            if (dag)
            {
                (dagAdj![(int)u] ??= new List<int>()).Add((int)v);
                indegree![(int)v]++;
            }
            if (strongAdj != null)
            {
                (strongAdj[(int)u] ??= new List<int>()).Add((int)v);
                (reverseStrongAdj![(int)v] ??= new List<int>()).Add((int)u);
            }
        }
        if (c.B("connected") && comps != 1) return "Đồ thị không liên thông.";
        if (strongAdj != null)
        {
            static int ReachableCount(List<int>?[] adjacency, int start, int vertexCount)
            {
                var visited = new bool[vertexCount + 1];
                var stack = new Stack<int>();
                visited[start] = true;
                stack.Push(start);
                int count = 0;
                while (stack.Count > 0)
                {
                    int u = stack.Pop();
                    count++;
                    if (adjacency[u] is not { } neighbors) continue;
                    foreach (int v in neighbors)
                        if (!visited[v]) { visited[v] = true; stack.Push(v); }
                }
                return count;
            }
            if (ReachableCount(strongAdj, 1, (int)n) != n || ReachableCount(reverseStrongAdj!, 1, (int)n) != n)
                return "Đồ thị không liên thông mạnh theo ràng buộc stronglyConnected.";
        }
        if (bipartite)
        {
            var colors = Enumerable.Repeat(-1, (int)n + 1).ToArray();
            var queue = new Queue<int>();
            for (int start = 1; start <= (int)n; start++)
            {
                if (colors[start] >= 0) continue;
                colors[start] = 0;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int u = queue.Dequeue();
                    if (colorAdj![u] is not { } neighbors) continue;
                    foreach (int v in neighbors)
                    {
                        if (colors[v] == colors[u]) return "Đồ thị không hai phía: có cạnh nối hai đỉnh cùng màu.";
                        if (colors[v] >= 0) continue;
                        colors[v] = 1 - colors[u];
                        queue.Enqueue(v);
                    }
                }
            }
        }
        if (dag)
        {
            var queue = new Queue<int>();
            for (int v = 1; v <= (int)n; v++) if (indegree![v] == 0) queue.Enqueue(v);
            int visited = 0;
            while (queue.Count > 0)
            {
                int u = queue.Dequeue(); visited++;
                if (dagAdj![u] is not { } neighbors) continue;
                foreach (int v in neighbors)
                    if (--indegree![v] == 0) queue.Enqueue(v);
            }
            if (visited != n) return "Đồ thị có chu trình, không phải DAG.";
        }
        return null;
    }

    public override ulong EstimateMaxBytes(Config c)
    {
        ulong per = 2 * (ulong)Util.LL(c.I("maxN")).Length + 2;
        if (c.B("weighted")) per += Util.MaxDigits(c.I("minW"), c.I("maxW")) + 1;
        ulong maxEdges = c.I("density") == 0
            ? (ulong)c.I("maxM")
            : (ulong)DensityEdges(c, c.I("maxN")).High;
        return maxEdges * per + 24;
    }
}
