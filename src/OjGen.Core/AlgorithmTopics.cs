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
            if (string.IsNullOrWhiteSpace(ExampleProblem)) return null;
            var problemCode = ExampleProblem.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            if (Source == "QBOJ")
                return $"http://qboj.zapto.org/problem/{problemCode}";
            if (Source == "LQDOJ")
                return $"https://lqdoj.edu.vn/problem/{problemCode.ToLowerInvariant()}";
            if (Source != "Codeforces") return null;

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
/// VNOI, Codeforces, QBOJ, and LQDOJ tags mapped to reusable OjGen input formats.
/// Tags are based on public problem directories; they are not problem solutions.
/// </summary>
public static class AlgorithmTopics
{
    public static IReadOnlyList<AlgorithmTopic> All { get; } = new AlgorithmTopic[]
    {
        new("Cờ vua", "Đếm nước đi của quân Mã", "chess_moves", "OjGen"),
        new("Cờ vua", "Đếm nước đi của quân cờ", "chess_piece_moves", "OjGen"),
        new("Cờ vua", "Liệt kê ô quân Mã có thể đi", "chess_knight_targets", "OjGen"),
        new("Cờ vua", "Kiểm tra Mã đi một nước", "chess_knight_reachability", "OjGen"),
        new("Cờ vua", "Đường đi ngắn nhất của quân Mã", "chess_knight_shortest", "OjGen"),

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

        // QBOJ problem-list labels collected from all 1,069 listed problems (22 pages).
        // Each label maps to the nearest input shape; the linked statement remains the authority.
        new("Duyệt", "Quay lui (backtr)", "array", "QBOJ", "hidden11 — Vũ điệu trăng đêm - Mã đi tuần"),
        new("Cơ bản", "Bài cơ bản (basic)", "custom", "QBOJ", "trontim — Trò chơi trốn tìm"),
        new("Tìm kiếm", "Tìm kiếm nhị phân (binary)", "array", "QBOJ", "thuhoach — Thu hoạch cây trồng"),
        new("Dãy và cửa sổ", "Dãy / dữ liệu biến thiên (chaotic)", "array", "QBOJ", "c3daycon — Câu 3. Dãy con"),
        new("Xử lý xâu", "Mã hóa / giải mã (codeendcode)", "string", "QBOJ", "hsg9qt20232024c2 — Mật thư"),
        new("Ma trận", "Mảng hai chiều (doublearr)", "matrix", "QBOJ", "thv — Tổng hình vuông"),
        new("Quy hoạch động", "Quy hoạch động (dynamic)", "array", "QBOJ", "dynamic71 — Phân tích chuỗi thành Palindrome"),
        new("Số học", "Estinum", "single_n", "QBOJ", "vannghe — Văn nghệ"),
        new("Tham lam", "Tham lam (greedy)", "array", "QBOJ", "tangqua1 — Tặng quà"),
        new("Xử lý xâu", "Hashing (Hash)", "string", "QBOJ"),
        new("Đồ thị & cấu trúc dữ liệu", "Map / đồ thị (map)", "graph", "QBOJ", "map02 — Biểu diễn đồ thị"),
        new("Mảng & đánh dấu", "Đánh dấu (mark)", "array", "QBOJ", "c2chuyenvng2025 — Đếm từ"),
        new("Xử lý xâu", "Đánh dấu ký tự (markstr)", "string", "QBOJ", "cndd — Chuỗi con đầy đủ"),
        new("Toán học", "Toán học (math)", "single_n", "QBOJ", "tgv2 — Tam giác vuông"),
        new("Dạng tổng quát", "Dạng thường (normal)", "custom", "QBOJ"),
        new("Số học & biểu diễn", "Hệ chữ số (Numerals)", "string", "QBOJ", "gtbt — Tính giá trị biểu thức"),
        new("Xử lý xâu", "Số lưu dưới dạng xâu (numstr)", "string", "QBOJ", "numstr12 — Tìm số lớn nhất xâu"),
        new("Tiền tố & đếm", "Đếm tiền tố (prefixcount)", "array", "QBOJ", "stk — Số tự kỷ"),
        new("Tiền tố & mảng", "Cực trị tiền tố (prefixmaxmin)", "array", "QBOJ", "gtmax — Giá trị lớn nhất"),
        new("Tiền tố & mảng", "Tổng tiền tố (prefixsum)", "prefix_sum_range", "QBOJ", "thv — Tổng hình vuông"),
        new("Dạng tổng quát", "Recipe", "custom", "QBOJ", "mrnguu13 — Biến đổi"),
        new("Mảng & đếm", "Quy tắc / đếm (rule)", "array", "QBOJ", "chiahetab — Tổng chia hết a,b"),
        new("Dãy và cửa sổ", "Cửa sổ trượt (slidwin)", "array", "QBOJ", "muaqua — Mua quà"),
        new("Sắp xếp", "Sắp xếp (Sort)", "array", "QBOJ", "dthcnmax — Diện tích hình chữ nhật lớn nhất"),
        new("Số học", "Số đặc biệt / nguyên tố (special num)", "array", "QBOJ", "prespecial10 — Tổng số nguyên tố"),
        new("Dãy & đoạn con", "Dãy con (subarr)", "array", "QBOJ", "pseq — Dãy con hoàn hảo"),
        new("Truy vấn", "Cập nhật đoạn con (subarr1)", "queries", "QBOJ", "update1 — Cập nhật đoạn"),
        new("Dãy và cửa sổ", "Hai con trỏ (two point)", "array", "QBOJ", "summax — Tổng đoạn lớn nhất"),
        new("Xử lý xâu", "Từ và văn bản (word)", "string", "QBOJ", "demtu8 — Đếm từ"),

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

        // Active problem-type tags from the LQDOJ problem directory.
        // Each entry links to one problem carrying that tag; input shapes are recommendations.
        LqdojTopic("2-sat", "CSES3312"),
        LqdojTopic("adhoc", "HDT"),
        LqdojTopic("aho-corasick", "APEXCUP26VL1B3"),
        LqdojTopic("AI", "CONNECT4"),
        LqdojTopic("array-list (mảng)", "26HSG9NANYENTHANH3"),
        LqdojTopic("articulation-point (khớp)", "GLTHT23R1MILKTEA"),
        LqdojTopic("backtrack (quay lui)", "KURSK"),
        LqdojTopic("beginner (nhập môn)", "BTHH"),
        LqdojTopic("bellman-ford", "CUP2025R6P5"),
        LqdojTopic("bfs-01", "TIMELINE2"),
        LqdojTopic("biconnected-component (song liên thông)", "PVHOI3CHUTRINH"),
        LqdojTopic("bignum", "OSICA"),
        LqdojTopic("binary-lifting", "CSES3409"),
        LqdojTopic("binary-search (chặt nhị phân)", "RIGHTLEFTLEFTLEFTLEFTRIGHTLEFT"),
        LqdojTopic("binary-search-parallel (chặt nhị phân song song)", "26OLP304K10BAIC"),
        LqdojTopic("bitmask", "CONNECT4"),
        LqdojTopic("bitset", "HSG12GLAIB5"),
        LqdojTopic("bitwise (phép toán bit)", "XORMAXTREEPATH"),
        LqdojTopic("block-cut-tree", "CONVOLUTIONPATH"),
        LqdojTopic("branch-bound (nhánh cận)", "CSES1625"),
        LqdojTopic("bridge (cầu)", "MINBRIDGE"),
        LqdojTopic("brute force", "HOAHOCCOBAN6"),
        LqdojTopic("casework (xử lý trường hợp)", "CARO3X3"),
        LqdojTopic("centroid", "CEOI26BIRDWATCHERS"),
        LqdojTopic("chinese-remainder (thặng dư Trung Hoa)", "ABCDEFG31"),
        LqdojTopic("combinatorics (tổ hợp)", "PRODUCTGCD"),
        LqdojTopic("constructive (xây dựng)", "US2026OPEN"),
        LqdojTopic("convex-hull (bao lồi)", "CSES3301"),
        LqdojTopic("counting (đếm phân phối)", "CA0110"),
        LqdojTopic("cycle (chu trình)", "CSES3398"),
        LqdojTopic("data structures (cấu trúc dữ liệu)", "DPCC"),
        LqdojTopic("deque", "LUKAMODRIC"),
        LqdojTopic("dfs/bfs/pfs", "CF383C"),
        LqdojTopic("dijkstra", "KURSK"),
        LqdojTopic("divide and conquer (chia để trị)", "26TI30BGRAPH"),
        LqdojTopic("dp-alien-trick", "LOINANGLUONG"),
        LqdojTopic("dp-bitmask (quy hoạch động bitmask)", "KURSK"),
        LqdojTopic("dp-connected-component", "2024FEBPLATINUM2"),
        LqdojTopic("dp-convexhull (quy hoạch động bao lồi)", "CSES3301"),
        LqdojTopic("dp-count (quy hoạch động đếm)", "CSES3232"),
        LqdojTopic("dp-dag (quy hoạch động trên DAG)", "HTCTVJ"),
        LqdojTopic("dp-digit (quy hoạch động chữ số)", "APEXCUP26VL1B3"),
        LqdojTopic("dp-dnc (quy hoạch động chia để trị)", "POKEMONLOINGUYEN"),
        LqdojTopic("dp-general (quy hoạch động cơ bản)", "BAGRATION"),
        LqdojTopic("dp-knuth (quy hoạch động Knuth)", "CSES3159"),
        LqdojTopic("dp-matrix (quy hoạch động ma trận)", "TILING"),
        LqdojTopic("dp-permutation (quy hoạch động hoán vị)", "CSES2421"),
        LqdojTopic("dp-slope (quy hoạch động slope-trick)", "JOI26C3SCARECROWS"),
        LqdojTopic("dp-sos (quy hoạch động SOS)", "CSES3141"),
        LqdojTopic("dp-tree (quy hoạch động trên cây)", "26TI30CONNET"),
        LqdojTopic("draw (vẽ)", "24STRA1"),
        LqdojTopic("dsu", "PHYSICEXTREMLYHARD"),
        LqdojTopic("dsu-roll-back", "26TI30BGRAPH"),
        LqdojTopic("euler-path (đường đi Euler)", "CSES1693"),
        LqdojTopic("euler-tour (đường đi Euler trên cây)", "CF383C"),
        LqdojTopic("extended-euclidean (Euclid mở rộng)", "ABCDEFG31"),
        LqdojTopic("fenwick-tree (BIT)", "CF383C"),
        LqdojTopic("fenwick-tree-2d (BIT2D)", "MD"),
        LqdojTopic("fft/ntt", "TTSKCDS"),
        LqdojTopic("flow-demands (luồng với điều kiện)", "CSES2131"),
        LqdojTopic("flow-general (luồng tổng quát)", "CSES3358"),
        LqdojTopic("flow-mincost (luồng chi phí nhỏ nhất)", "CHIPSCHALLENGE"),
        LqdojTopic("floyd-warshall", "HSG12GLAIB2"),
        LqdojTopic("for, while (cấu trúc lặp lồng nhau)", "LISTREMOVAL"),
        LqdojTopic("for, while (cấu trúc lặp)", "W07C"),
        LqdojTopic("function-graph (đồ thị hàm)", "RANDOMGAME"),
        LqdojTopic("game-ad-hoc", "IOI26D1P3"),
        LqdojTopic("game-minimax", "CONNECT4"),
        LqdojTopic("game-nim", "THSTMNBDAY1PROBBLECTURE"),
        LqdojTopic("game-sprague-grundy", "2024FEBBRONZE1"),
        LqdojTopic("gauss-elimination (khử Gauss)", "HOAHOCCOBAN2"),
        LqdojTopic("genetic (thuật toán di truyền)", "OLP4CK3C"),
        LqdojTopic("geometry (hình học)", "TRIANGLEANDCIRCLE"),
        LqdojTopic("graph (đồ thị)", "CHIPSCHALLENGE"),
        LqdojTopic("greedy (tham lam)", "CHIPSCHALLENGE"),
        LqdojTopic("hamilton-cycle", "CSES1690"),
        LqdojTopic("hashing", "CSES3222"),
        LqdojTopic("heavy-light-decomposition", "BAGRATION"),
        LqdojTopic("heuristic", "CSES3424"),
        LqdojTopic("if-else (câu lệnh rẽ nhánh)", "TNPTB2"),
        LqdojTopic("implementation (cài đặt)", "HDT"),
        LqdojTopic("inclusion-exclusion (bao hàm loại trừ)", "ABCDEFG36"),
        LqdojTopic("indefinite", "ONTAPTHT1"),
        LqdojTopic("interactive", "CONNECT4"),
        LqdojTopic("inversive-geometry", "ICPC21ROUNDEDCONVEX"),
        LqdojTopic("kmp", "STRINGCONSMACHINE"),
        LqdojTopic("kruskal", "26OLP304K10BAIC"),
        LqdojTopic("lagrange-interpolating (nội suy Lagrange)", "BUILDTREE"),
        LqdojTopic("lca (tổ tiên chung gần nhất)", "CEOI26BIRDWATCHERS"),
        LqdojTopic("lichao-tree", "TVGTLNTDT"),
        LqdojTopic("linked-list (danh sách liên kết)", "HSG12GLAIB1"),
        LqdojTopic("local-search", "OLP7SC4"),
        LqdojTopic("manacher", "CSES3138"),
        LqdojTopic("matching-bipartie (cặp ghép hai phía)", "CSES3358"),
        LqdojTopic("matching-general (cặp ghép)", "CAPGHEPDOTHI"),
        LqdojTopic("matching-hall (định lý Hall)", "LQDOJ2023R3BUCKET"),
        LqdojTopic("matching-hungarian (thuật toán Hungary)", "FAREWELL"),
        LqdojTopic("matching-konig (định lý Konig)", "CSES1709"),
        LqdojTopic("math (toán học)", "BTHH"),
        LqdojTopic("math-venn-diagram (biểu đồ Venn)", "ABCDEFG13"),
        LqdojTopic("matrix multiplication (nhân ma trận)", "USSR09"),
        LqdojTopic("meet-in-the-middle (duyệt phân tập)", "CSES3355"),
        LqdojTopic("minimum-spanning-tree (cây khung nhỏ nhất)", "26TI30MST"),
        LqdojTopic("mo-general (thuật toán Mo)", "26TS10VPHTTC"),
        LqdojTopic("mo-tree (Mo trên cây)", "2024DNA3"),
        LqdojTopic("mo-update (Mo với cập nhật)", "DIV3D21"),
        LqdojTopic("mobius", "MAGELLANCT1B5"),
        LqdojTopic("modular (số học modulo)", "PRODUCTGCD"),
        LqdojTopic("modular-inverse (nghịch đảo modulo)", "USSR08"),
        LqdojTopic("monotonic-queue (hàng đợi đơn điệu)", "NHOMHOCTAPNOIBAT"),
        LqdojTopic("Nhập Xuất Dữ Liệu", "CPPCB0801"),
        LqdojTopic("number theory (số học)", "TONGDU"),
        LqdojTopic("offline", "26TI30MST"),
        LqdojTopic("Olympiad", "GCJ22FINALSLIDEPARADE"),
        LqdojTopic("optimization (tối ưu hóa)", "USSR04"),
        LqdojTopic("others", "TS10CT15B"),
        LqdojTopic("output-only", "IOI25TRIPLES"),
        LqdojTopic("picks-theorem", "CSES2193"),
        LqdojTopic("planar-graph", "APIO17RAINBOW"),
        LqdojTopic("polygon-triangulation", "LQDOJ2022R7TRICOVER"),
        LqdojTopic("prefix-sum (mảng tiền tố)", "BEAUTIFULARRAY"),
        LqdojTopic("primality-test (kiểm tra số nguyên tố)", "SNT02"),
        LqdojTopic("probability (xác suất)", "KVLTVDH"),
        LqdojTopic("queue (hàng đợi)", "CSES3152"),
        LqdojTopic("randomized (thuật toán ngẫu nhiên)", "CSES3424"),
        LqdojTopic("recursion (đệ quy)", "DOANMK"),
        LqdojTopic("rmq (truy vấn đoạn)", "DHHH"),
        LqdojTopic("rmq-2d (truy vấn đoạn 2D)", "26TS10BNI3"),
        LqdojTopic("scc (liên thông mạnh)", "CSES3312"),
        LqdojTopic("segtree-2d (cây phân đoạn 2D)", "LQDOJ22FRINRANGE"),
        LqdojTopic("segtree-beats", "BIENDOIMODULODOAN"),
        LqdojTopic("segtree-dynamic", "TVGTLNTDT"),
        LqdojTopic("segtree-general (cây phân đoạn)", "BAGRATION"),
        LqdojTopic("segtree-persistent", "KINGDOMOFTIME"),
        LqdojTopic("segtree-walk", "MAGELLANCT1B4"),
        LqdojTopic("sieve (sàng nguyên tố)", "26HSG9NANYENTHANH2"),
        LqdojTopic("sieve-segment", "26HSG9HYE5"),
        LqdojTopic("small-to-large", "DPCC"),
        LqdojTopic("sorting (sắp xếp)", "26HSG9NANYENTHANH2"),
        LqdojTopic("sparse-table", "DHHH"),
        LqdojTopic("sqrt-decomposition (chia căn)", "TONGDU"),
        LqdojTopic("stack (ngăn xếp)", "CALCULATOR01"),
        LqdojTopic("stl", "CA0109"),
        LqdojTopic("string (chuỗi)", "HDT"),
        LqdojTopic("suffix-array (mảng hậu tố)", "APEXCUP26VL2B3"),
        LqdojTopic("sweep-line", "JOI26SFJEWELER"),
        LqdojTopic("tarjan", "BOMBS"),
        LqdojTopic("ternary-search (tìm kiếm tam phân)", "TS10QUANGNING2026B3"),
        LqdojTopic("topo-sort (sắp xếp topo)", "HTCTVJ"),
        LqdojTopic("tortoise-hare", "OLP7PRKC2"),
        LqdojTopic("treap/splay", "CFSUMMERVACATION"),
        LqdojTopic("tree (cây)", "CF383C"),
        LqdojTopic("tree-isomorphism", "SINGULARITY"),
        LqdojTopic("trie", "XLCSH"),
        LqdojTopic("two-pointers (hai con trỏ)", "26HSG9NANYENTHANH3"),
        LqdojTopic("z-function", "25TT11MATCH"),
    };

    private static AlgorithmTopic LqdojTopic(string tag, string exampleProblem) =>
        new(LqdojCategory(tag), tag, LqdojRecommendedProblemId(tag), "LQDOJ", exampleProblem);

    private static string LqdojCategory(string tag)
    {
        var key = tag.ToLowerInvariant();
        if (key.StartsWith("dp-", StringComparison.Ordinal) || key == "dp-general") return "Quy hoạch động";
        if (IsStringTopic(key)) return "Xử lý xâu";
        if (IsGeometryTopic(key)) return "Hình học";
        if (IsTreeTopic(key)) return "Cây & truy vấn trên cây";
        if (IsGraphTopic(key)) return "Lý thuyết đồ thị";
        if (key.StartsWith("game-", StringComparison.Ordinal)) return "Lý thuyết trò chơi";
        if (IsDataStructureTopic(key)) return "Cấu trúc dữ liệu";
        if (key.Contains("brute force", StringComparison.Ordinal) || key.Contains("backtrack", StringComparison.Ordinal) ||
            key.Contains("branch-bound", StringComparison.Ordinal) || key.Contains("meet-in-the-middle", StringComparison.Ordinal) ||
            key.Contains("recursion", StringComparison.Ordinal)) return "Duyệt";
        if (IsMathTopic(key)) return "Toán học";
        if (key.Contains("greedy", StringComparison.Ordinal)) return "Tham lam";
        if (IsArrayTopic(key)) return "Dãy, tìm kiếm & sắp xếp";
        return "Kĩ năng khác";
    }

    private static string LqdojRecommendedProblemId(string tag)
    {
        var key = tag.ToLowerInvariant();
        if (IsTreeTopic(key)) return "tree";
        if (IsGraphTopic(key)) return "graph";
        if (IsStringTopic(key)) return "string";
        if (IsGeometryTopic(key)) return "pairs";
        if (key.Contains("matrix", StringComparison.Ordinal) || key.Contains("2d", StringComparison.Ordinal)) return "matrix";
        if (key.Contains("fft/ntt", StringComparison.Ordinal)) return "array";
        if (key.StartsWith("dp-", StringComparison.Ordinal))
            return key.Contains("digit", StringComparison.Ordinal) ? "single_n" : "array";
        if (IsDataStructureTopic(key)) return "queries";
        if (IsMathTopic(key)) return "single_n";
        if (IsArrayTopic(key) || key.StartsWith("game-", StringComparison.Ordinal)) return "array";
        return "custom";
    }

    private static bool IsStringTopic(string key) =>
        key.Contains("string", StringComparison.Ordinal) || key.Contains("hash", StringComparison.Ordinal) ||
        key.Contains("aho-corasick", StringComparison.Ordinal) || key is "bignum" or "kmp" or "manacher" or "trie" or "z-function";

    private static bool IsGeometryTopic(string key) =>
        key.Contains("geometry", StringComparison.Ordinal) || key.Contains("convex-hull", StringComparison.Ordinal) ||
        key.Contains("polygon", StringComparison.Ordinal) || key.Contains("voronoi", StringComparison.Ordinal) ||
        key.Contains("picks-theorem", StringComparison.Ordinal) || key.Contains("inversive-geometry", StringComparison.Ordinal);

    private static bool IsTreeTopic(string key) =>
        key is "tree (cây)" or "tree-isomorphism" or "dp-tree (quy hoạch động trên cây)" or
            "binary-lifting" or "heavy-light-decomposition" or "lca (tổ tiên chung gần nhất)" or "centroid" or
            "euler-tour (đường đi Euler trên cây)" or "mo-tree (Mo trên cây)" or "small-to-large";

    private static bool IsGraphTopic(string key) =>
        key.Contains("graph", StringComparison.Ordinal) || key.Contains("flow-", StringComparison.Ordinal) ||
        key.Contains("matching-", StringComparison.Ordinal) || key.Contains("dijkstra", StringComparison.Ordinal) ||
        key.Contains("bellman-ford", StringComparison.Ordinal) || key.Contains("bfs-01", StringComparison.Ordinal) ||
        key.Contains("biconnected", StringComparison.Ordinal) || key.Contains("articulation", StringComparison.Ordinal) ||
        key.Contains("bridge", StringComparison.Ordinal) || key.Contains("dfs/bfs/pfs", StringComparison.Ordinal) ||
        key.Contains("block-cut-tree", StringComparison.Ordinal) || key.Contains("hamilton-cycle", StringComparison.Ordinal) ||
        key.Contains("euler-path", StringComparison.Ordinal) || key.Contains("floyd-warshall", StringComparison.Ordinal) ||
        key.Contains("kruskal", StringComparison.Ordinal) || key.Contains("minimum-spanning-tree", StringComparison.Ordinal) ||
        key.Contains("scc", StringComparison.Ordinal) || key.Contains("tarjan", StringComparison.Ordinal) ||
        key.Contains("topo-sort", StringComparison.Ordinal) || key is "2-sat" or "cycle (chu trình)" or "dsu" or "dsu-roll-back";

    private static bool IsDataStructureTopic(string key) =>
        key.Contains("data structures", StringComparison.Ordinal) || key.Contains("segtree", StringComparison.Ordinal) ||
        key.Contains("fenwick", StringComparison.Ordinal) || key.Contains("rmq", StringComparison.Ordinal) ||
        key.Contains("sparse-table", StringComparison.Ordinal) || key.Contains("mo-", StringComparison.Ordinal) ||
        key.Contains("sqrt-decomposition", StringComparison.Ordinal) || key.Contains("monotonic-queue", StringComparison.Ordinal) ||
        key is "deque" or "queue (hàng đợi)" or "stack (ngăn xếp)" or "stl" or "treap/splay" or "offline" or "lichao-tree";

    private static bool IsMathTopic(string key) =>
        key.Contains("math", StringComparison.Ordinal) || key.Contains("number theory", StringComparison.Ordinal) ||
        key.Contains("combinatorics", StringComparison.Ordinal) || key.Contains("chinese-remainder", StringComparison.Ordinal) ||
        key.Contains("euclidean", StringComparison.Ordinal) || key.Contains("modular", StringComparison.Ordinal) ||
        key.Contains("sieve", StringComparison.Ordinal) || key.Contains("primality", StringComparison.Ordinal) ||
        key.Contains("mobius", StringComparison.Ordinal) || key.Contains("fft/ntt", StringComparison.Ordinal) ||
        key.Contains("lagrange", StringComparison.Ordinal) || key.Contains("gauss-elimination", StringComparison.Ordinal) ||
        key.Contains("probability", StringComparison.Ordinal) || key.Contains("inclusion-exclusion", StringComparison.Ordinal);

    private static bool IsArrayTopic(string key) =>
        key.Contains("array-list", StringComparison.Ordinal) || key.Contains("sorting", StringComparison.Ordinal) ||
        key.Contains("prefix-sum", StringComparison.Ordinal) || key.Contains("two-pointers", StringComparison.Ordinal) ||
        key.Contains("binary-search", StringComparison.Ordinal) || key.Contains("ternary-search", StringComparison.Ordinal) ||
        key.Contains("bitmask", StringComparison.Ordinal) || key.Contains("bitset", StringComparison.Ordinal) ||
        key.Contains("bitwise", StringComparison.Ordinal) || key.Contains("counting", StringComparison.Ordinal) ||
        key.Contains("fft/ntt", StringComparison.Ordinal) ||
        key.Contains("brute force", StringComparison.Ordinal) || key.Contains("backtrack", StringComparison.Ordinal) ||
        key.Contains("branch-bound", StringComparison.Ordinal) || key.Contains("meet-in-the-middle", StringComparison.Ordinal) ||
        key.Contains("divide and conquer", StringComparison.Ordinal) || key.Contains("randomized", StringComparison.Ordinal) ||
        key.Contains("recursion", StringComparison.Ordinal) || key.Contains("greedy", StringComparison.Ordinal) ||
        key.Contains("linked-list", StringComparison.Ordinal) || key == "dp-general (quy hoạch động cơ bản)";
}
