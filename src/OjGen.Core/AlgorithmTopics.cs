namespace OjGen.Core;

/// <summary>
/// A public algorithm tag with the closest input generator available in OjGen.
/// The recommended generator is only a starting point: a tag does not define
/// the input format or the full constraints of an individual problem.
/// </summary>
public sealed record AlgorithmTopic(
    string Category,
    string Name,
    string RecommendedProblemId,
    string Source = "VNOI",
    string? ExampleProblem = null)
{
    public string DisplayName => string.IsNullOrEmpty(Category)
        ? $"{Name} — {Source}"
        : $"{Name} — {Source} · {Category}";

    public string? ExampleProblemUrl
    {
        get
        {
            if (Source != "Codeforces" || string.IsNullOrWhiteSpace(ExampleProblem)) return null;
            var problemCode = ExampleProblem.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            var indexStart = 0;
            while (indexStart < problemCode.Length && char.IsAsciiDigit(problemCode[indexStart])) indexStart++;
            if (indexStart == 0 || indexStart == problemCode.Length ||
                !int.TryParse(problemCode[..indexStart], out var contestId)) return null;

            var problemIndex = problemCode[indexStart..];
            return $"https://codeforces.com/problemset/problem/{contestId}/{problemIndex}";
        }
    }
}

/// <summary>
/// VNOI and Codeforces algorithm tags mapped to reusable OjGen input formats.
/// Tags are based on public problem directories; they are not problem solutions.
/// </summary>
public static class AlgorithmTopics
{
    public static IReadOnlyList<AlgorithmTopic> All { get; } = new AlgorithmTopic[]
    {
        // Lý thuyết đồ thị
        new("Lý thuyết Đồ thị", "DFS / BFS", "graph"),
        new("Lý thuyết Đồ thị", "Chu trình Euler", "graph"),
        new("Lý thuyết Đồ thị", "Đường đi ngắn nhất - Dijkstra", "graph"),
        new("Lý thuyết Đồ thị", "Cây khung nhỏ nhất", "graph"),
        new("Lý thuyết Đồ thị", "2-satisfiability", "graph"),
        new("Lý thuyết Đồ thị", "LCA", "tree"),
        new("Lý thuyết Đồ thị", "Đồ thị", "graph"),
        new("Lý thuyết Đồ thị", "Chu trình", "graph"),
        new("Lý thuyết Đồ thị", "Thành phần song liên thông", "graph"),
        new("Lý thuyết Đồ thị", "Thành phần liên thông mạnh", "graph"),
        new("Lý thuyết Đồ thị", "Topo sort", "graph"),
        new("Lý thuyết Đồ thị", "Euler tour trên cây", "tree"),
        new("Lý thuyết Đồ thị", "Centroid", "tree"),
        new("Lý thuyết Đồ thị", "Tree isomorphism (Cây đồng dạng)", "tree"),
        new("Lý thuyết Đồ thị", "Đường đi ngắn nhất - Floyd", "graph"),
        new("Lý thuyết Đồ thị", "BFS 01", "graph"),
        new("Lý thuyết Đồ thị", "Disjoint set (DSU)", "graph"),
        new("Lý thuyết Đồ thị", "Boruvka", "graph"),
        new("Lý thuyết Đồ thị", "Maximum Clique", "graph"),
        new("Lý thuyết Đồ thị", "DSU rollback", "graph"),
        new("Lý thuyết Đồ thị", "Đồ thị đặc biệt - Xương rồng", "graph"),
        new("Lý thuyết Đồ thị", "Đồ thị đặc biệt - Function graph", "graph"),
        new("Lý thuyết Đồ thị", "Cầu với khớp", "graph"),

        // Cấu trúc dữ liệu
        new("Cấu trúc dữ liệu", "C++ STL (Heap, Set, Map, ...)", "queries"),
        new("Cấu trúc dữ liệu", "Balanced BST (cây nhị phân cân bằng)", "queries"),
        new("Cấu trúc dữ liệu", "Mảng cộng dồn", "prefix_sum_range"),
        new("Cấu trúc dữ liệu", "Segment Tree (Interval Tree)", "queries"),
        new("Cấu trúc dữ liệu", "Heavy Light Decomposition", "tree"),
        new("Cấu trúc dữ liệu", "Monotonic Queue", "array"),
        new("Cấu trúc dữ liệu", "Segment Tree Walk", "queries"),
        new("Cấu trúc dữ liệu", "Bitset", "array"),
        new("Cấu trúc dữ liệu", "Fenwick Tree 2D", "matrix"),
        new("Cấu trúc dữ liệu", "Segment Tree 2D", "matrix"),
        new("Cấu trúc dữ liệu", "RMQ", "array"),
        new("Cấu trúc dữ liệu", "Fenwick Tree (Binary Indexed Tree)", "queries"),
        new("Cấu trúc dữ liệu", "Binary Lifting", "tree"),
        new("Cấu trúc dữ liệu", "Sparse table", "array"),
        new("Cấu trúc dữ liệu", "Persistent Segment Tree", "queries"),
        new("Cấu trúc dữ liệu", "Segment Tree đoạn thẳng (Li Chao)", "pairs"),
        new("Cấu trúc dữ liệu", "GNU C++ PBDS (ordered set)", "queries"),
        new("Cấu trúc dữ liệu", "Segment Tree Beats", "queries"),
        new("Cấu trúc dữ liệu", "Block cut tree (graph)", "graph"),
        new("Cấu trúc dữ liệu", "Xử lý offline", "queries"),

        // Xử lý xâu
        new("Xử lý xâu", "Hashing", "string"),
        new("Xử lý xâu", "Trie", "string"),
        new("Xử lý xâu", "Suffix Array / Suffix Automaton / Suffix Tree", "string"),
        new("Xử lý xâu", "KMP", "string"),
        new("Xử lý xâu", "Z-function", "string"),
        new("Xử lý xâu", "Manacher", "string"),
        new("Xử lý xâu", "Palindrome Tree", "string"),
        new("Xử lý xâu", "Aho Corasick", "string"),

        // Quy hoạch động
        new("Quy hoạch động", "Quy hoạch động", "array"),
        new("Quy hoạch động", "Quy hoạch động chữ số", "single_n"),
        new("Quy hoạch động", "Quy hoạch động bitmask", "array"),
        new("Quy hoạch động", "Quy hoạch động bao lồi", "pairs"),
        new("Quy hoạch động", "Quy hoạch động thứ tự từ điển", "string"),
        new("Quy hoạch động", "Quy hoạch động trên cây", "tree"),
        new("Quy hoạch động", "Quy hoạch động Knapsack trên cây", "tree"),
        new("Quy hoạch động", "Quy hoạch động trên DAG", "graph"),
        new("Quy hoạch động", "Quy hoạch động đếm", "array"),
        new("Quy hoạch động", "Quy hoạch động chia để trị", "array"),
        new("Quy hoạch động", "Quy hoạch động hoán vị", "array"),
        new("Quy hoạch động", "Quy hoạch động bitmask tổng tập con", "array"),
        new("Quy hoạch động", "Quy hoạch động đổi nhãn", "graph"),
        new("Quy hoạch động", "Knuth Optimization", "queries"),
        new("Quy hoạch động", "Open Close Interval Trick", "pairs"),
        new("Quy hoạch động", "Alien Trick", "array"),
        new("Quy hoạch động", "Slope Trick", "array"),

        // Kĩ năng khác
        new("Kĩ năng khác", "Tìm kiếm nhị phân (Binary search)", "array"),
        new("Kĩ năng khác", "Nhân ma trận", "matrix"),
        new("Kĩ năng khác", "Xử lý số lớn", "single_n"),
        new("Kĩ năng khác", "Rời rạc hóa", "array"),
        new("Kĩ năng khác", "Ad hoc (không thuộc thể loại nào)", "custom"),
        new("Kĩ năng khác", "Sweep line", "pairs"),
        new("Kĩ năng khác", "Divide & Conquer (Chia để trị)", "array"),
        new("Kĩ năng khác", "2 con trỏ", "array"),
        new("Kĩ năng khác", "Parallel Binary Search", "queries"),
        new("Kĩ năng khác", "Gộp set (Small to Large)", "tree"),
        new("Kĩ năng khác", "Constructive", "array"),
        new("Kĩ năng khác", "Interactive", "custom"),
        new("Kĩ năng khác", "Random", "array"),
        new("Kĩ năng khác", "Xét trường hợp", "custom"),
        new("Kĩ năng khác", "Cài đặt khó", "custom"),
        new("Kĩ năng khác", "Mảng hằng", "array"),
        new("Kĩ năng khác", "Lật ngược bài toán", "array"),
        new("Kĩ năng khác", "Cài đặt", "custom"),
        new("Kĩ năng khác", "Tìm kiếm tam phân (Ternary search)", "array"),

        // Hình học
        new("Hình học", "Hình học", "pairs"),
        new("Hình học", "Bao lồi", "pairs"),
        new("Hình học", "Inversive Geometry", "pairs"),

        // Toán học
        new("Toán học", "Số học", "single_n"),
        new("Toán học", "Khử Gauss", "matrix"),
        new("Toán học", "Fast Fourier Transform (FFT)", "array"),
        new("Toán học", "Tổ hợp", "single_n"),
        new("Toán học", "Bitwise", "array"),
        new("Toán học", "Toán - adhoc", "single_n"),
        new("Toán học", "Nội suy Lagrange", "single_n"),
        new("Toán học", "Extended GCD", "single_n"),
        new("Toán học", "Bao hàm loại trừ (Inclusion Exclusion)", "single_n"),
        new("Toán học", "Hàm Mobius", "array"),
        new("Toán học", "Xác suất", "array"),
        new("Toán học", "Hàm nhân tính", "array"),
        new("Toán học", "Sàng nguyên tố", "array"),
        new("Toán học", "Thặng dư Trung Hoa", "single_n"),

        // Luồng và cặp ghép
        new("Luồng & Cặp ghép", "Luồng", "graph"),
        new("Luồng & Cặp ghép", "Cặp ghép", "graph"),
        new("Luồng & Cặp ghép", "Cặp ghép có trọng số", "graph"),
        new("Luồng & Cặp ghép", "Luồng mincost", "graph"),
        new("Luồng & Cặp ghép", "Định lý Hall", "graph"),
        new("Luồng & Cặp ghép", "Luồng cận dưới", "graph"),
        new("Luồng & Cặp ghép", "Cặp ghép trên đồ thị thường", "graph"),

        // Tham lam
        new("Tham lam", "Tham lam", "array"),
        new("Tham lam", "Tham lam - Moore", "array"),
        new("Tham lam", "Tham lam - Exchange argument", "array"),

        // Lý thuyết trò chơi
        new("Lý thuyết trò chơi", "Lý thuyết trò chơi - Grundy", "single_n"),
        new("Lý thuyết trò chơi", "Lý thuyết trò chơi - Nim", "array"),
        new("Lý thuyết trò chơi", "Lý thuyết trò chơi - Minimax", "array"),
        new("Lý thuyết trò chơi", "Lý thuyết trò chơi - Adhoc", "single_n"),

        // Duyệt
        new("Duyệt", "Duyệt", "array"),
        new("Duyệt", "Đệ quy (backtrack)", "array"),
        new("Duyệt", "Nhánh cận (branch & bound)", "array"),
        new("Duyệt", "Chia đôi tập / Meet in the middle", "array"),

        // Chia căn
        new("Chia căn (Sqrt Decomposition)", "Chia block", "queries"),
        new("Chia căn (Sqrt Decomposition)", "Chia theo truy vấn", "queries"),
        new("Chia căn (Sqrt Decomposition)", "Chia 2 cách làm", "queries"),
        new("Chia căn (Sqrt Decomposition)", "Mo algorithm", "queries"),
        new("Chia căn (Sqrt Decomposition)", "Baby step giant step", "single_n"),
        new("Chia căn (Sqrt Decomposition)", "Mo algorithm có cập nhật", "queries"),

        // Codeforces problemset tags and representative public problems.
        new("", "2-sat", "graph", "Codeforces", "1239D — Catowice City"),
        new("", "binary search", "array", "Codeforces", "2277D1 — Number Operating (Easy Version)"),
        new("", "bitmasks", "array", "Codeforces", "2277D2 — Number Operating (Hard Version)"),
        new("", "brute force", "array", "Codeforces", "2277D1 — Number Operating (Easy Version)"),
        new("", "chinese remainder theorem", "single_n", "Codeforces", "2264D — Dr. Agos's Dark Mode"),
        new("", "combinatorics", "single_n", "Codeforces", "2276G — Elimination of Players"),
        new("", "constructive algorithms", "array", "Codeforces", "2277E — Tree Guessing"),
        new("", "data structures", "queries", "Codeforces", "2277F — Token Re-Moving"),
        new("", "dfs and similar", "graph", "Codeforces", "2277F — Token Re-Moving"),
        new("", "divide and conquer", "array", "Codeforces", "2277D2 — Number Operating (Hard Version)"),
        new("", "dp", "array", "Codeforces", "2277D2 — Number Operating (Hard Version)"),
        new("", "dsu", "graph", "Codeforces", "2277F — Token Re-Moving"),
        new("", "expression parsing", "string", "Codeforces", "2052E — Expression Correction"),
        new("", "fft", "array", "Codeforces", "2268E — Kia Kio and Tree of Life"),
        new("", "flows", "graph", "Codeforces", "1082G — Petya and Graph"),
        new("", "games", "single_n", "Codeforces", "2271B — MEX Game"),
        new("", "geometry", "pairs", "Codeforces", "2275A — In Search of Convenience"),
        new("", "graph matchings", "graph", "Codeforces", "1239D — Catowice City"),
        new("", "graphs", "graph", "Codeforces", "2276D — Caveman"),
        new("", "greedy", "array", "Codeforces", "2277B — Sequence Decreasing"),
        new("", "hashing", "string", "Codeforces", "2275F — Tea Blend"),
        new("", "implementation", "custom", "Codeforces", "2276B — Replace and Compress"),
        new("", "interactive", "custom", "Codeforces", "2277E — Tree Guessing"),
        new("", "math", "single_n", "Codeforces", "2276G — Elimination of Players"),
        new("", "matrices", "matrix", "Codeforces", "2273C — Matrix Puzzle"),
        new("", "meet-in-the-middle", "array", "Codeforces", "888E — Maximum Subsequence"),
        new("", "number theory", "single_n", "Codeforces", "2276C — Subarrays"),
        new("", "probabilities", "array", "Codeforces", "2125D — Segments Covering"),
        new("", "schedules", "pairs", "Codeforces", "2237A — Destroying Towers"),
        new("", "shortest paths", "graph", "Codeforces", "1473E — Minimum Path"),
        new("", "sortings", "array", "Codeforces", "2275G — Copper Squander"),
        new("", "string suffix structures", "string", "Codeforces", "2209E — A Trivial String Problem"),
        new("", "strings", "string", "Codeforces", "2276B — Replace and Compress"),
        new("", "ternary search", "array", "Codeforces", "2275G — Copper Squander"),
        new("", "trees", "tree", "Codeforces", "2277E — Tree Guessing"),
        new("", "two pointers", "array", "Codeforces", "2271D — Target Infection"),
        new("", "communication", "custom", "Codeforces", "2255C — Even If the World Turns"),
    };
}
