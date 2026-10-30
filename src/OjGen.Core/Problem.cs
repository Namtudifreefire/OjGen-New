// Problem.cs - Giao diện thống nhất cho mọi dạng bài (module) sinh test.
//
// Mỗi module cài đặt một lớp con của Problem. Phần lõi chỉ làm việc qua giao diện này:
//   Params          -> danh sách tham số (form trên giao diện và dòng lệnh --key value)
//   ValidateConfig  -> kiểm tra cấu hình trước khi sinh
//   Generate        -> sinh nội dung MỘT file input
//   ValidateInput   -> kiểm tra lại file input đúng định dạng và đúng giới hạn
// Các hàm kiểm tra trả về null nếu hợp lệ, ngược lại là thông báo lỗi.
using System.Text;

namespace OjGen.Core;

public abstract class Problem
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    /// <summary>Nhóm cấu trúc input chính, dùng để chia nhóm các nhánh trong GUI.</summary>
    public virtual string MainCategory => "Khác";
    public abstract string FormatDescription { get; }
    public abstract IReadOnlyList<ParamSpec> Params { get; }

    /// <summary>Cấu hình mặc định lấy từ Params.</summary>
    public virtual Config DefaultConfig()
    {
        var c = new Config();
        foreach (var p in Params)
        {
            if (p.Type == ParamType.Text) c.SetText(p.Key, p.DefText);
            else c.Set(p.Key, p.Def);
        }
        return c;
    }

    /// <summary>Hoàn thiện cấu hình từ dòng lệnh (vd suy ra n từ --bounds). explicitKeys là các tham số người dùng đã truyền.</summary>
    public virtual void FinalizeCliConfig(Config cfg, ISet<string> explicitKeys) { }

    /// <summary>
    /// Giao diện gọi sau mỗi lần người dùng sửa một tham số, để module tự đồng bộ các tham số liên quan
    /// (vd custom: dán bounds thì n tự bằng số vị trí). Mặc định không làm gì.
    /// </summary>
    public virtual void AdjustConfig(Config cfg, string changedKey) { }

    /// <summary>Kiểm tra logic giữa các tham số. Miền [Lo, Hi] của từng tham số đã được kiểm tra trước.</summary>
    public abstract string? ValidateConfig(Config cfg);

    /// <summary>Sinh nội dung một file input (kết thúc bằng '\n', xuống dòng kiểu LF).</summary>
    public abstract string Generate(Rng rng, Config cfg);

    /// <summary>Xác thực một file input theo đúng định dạng và giới hạn của cấu hình.</summary>
    public abstract string? ValidateInput(string input, Config cfg);

    /// <summary>Ước lượng dung lượng tối đa của một file input (byte), 0 = không rõ.</summary>
    public virtual ulong EstimateMaxBytes(Config cfg) => 0;

    /// <summary>Mô tả cấu hình (ghi vào file thông tin và hiển thị cho người dùng).</summary>
    public virtual string DescribeConfig(Config cfg)
    {
        var sb = new StringBuilder();
        foreach (var p in Params)
        {
            sb.Append("  - ").Append(p.Label);
            if (!p.Label.Contains("(" + p.Key + ")")) sb.Append(" (").Append(p.Key).Append(')');
            sb.Append(": ");
            if (p.Type == ParamType.Text) sb.Append('"').Append(cfg.T(p.Key)).Append('"');
            else if (p.Type == ParamType.Bool) sb.Append(cfg.B(p.Key) ? "có" : "không");
            else sb.Append(Util.LL(cfg.I(p.Key)));
            sb.Append('\n');
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------ hỗ trợ chung

    /// <summary>Kiểm tra miền giá trị của từng tham số theo ParamSpec.</summary>
    public static string? CheckParamRanges(Problem p, Config cfg)
    {
        foreach (var ps in p.Params)
        {
            if (ps.Type == ParamType.Int)
            {
                long v = cfg.I(ps.Key);
                if (v < ps.Lo || v > ps.Hi)
                    return $"Tham số '{ps.Key}' ({ps.Label}) = {Util.LL(v)} nằm ngoài miền cho phép [{Util.LL(ps.Lo)}, {Util.LL(ps.Hi)}].";
            }
            else if (ps.Type == ParamType.Bool)
            {
                long v = cfg.I(ps.Key);
                if (v != 0 && v != 1) return $"Tham số '{ps.Key}' chỉ nhận 0 hoặc 1.";
            }
        }
        return null;
    }

    /// <summary>
    /// Tách input thành các dòng, mỗi dòng là danh sách token. Bắt buộc: kết thúc bằng '\n',
    /// không có '\r', không có dòng trống, các token cách nhau đúng MỘT dấu cách,
    /// không có khoảng trắng đầu/cuối dòng.
    /// </summary>
    public static string? SplitInputLines(string input, out List<string[]> lines)
    {
        lines = new List<string[]>();
        if (input.Length == 0) return "File input rỗng.";
        if (input[^1] != '\n') return "File input không kết thúc bằng ký tự xuống dòng.";
        int start = 0, lineNo = 0;
        while (start < input.Length)
        {
            int end = input.IndexOf('\n', start);
            string line = input.Substring(start, end - start);
            lineNo++;
            string where = $"Dòng {lineNo}: ";
            if (line.Length == 0) return where + "dòng trống không hợp lệ.";
            if (line.Contains('\r')) return where + "chứa ký tự \\r (CRLF).";
            if (line[0] == ' ' || line[^1] == ' ') return where + "có khoảng trắng thừa ở đầu/cuối dòng.";
            if (line.Contains("  ")) return where + "có hai dấu cách liên tiếp.";
            if (line.Contains('\t')) return where + "chứa ký tự tab.";
            lines.Add(line.Split(' '));
            start = end + 1;
        }
        return null;
    }

    /// <summary>Đọc token số nguyên và kiểm tra nằm trong [lo, hi].</summary>
    public static string? ReadIntToken(string tok, long lo, long hi, string what, out long value)
    {
        if (!Util.ParseStrictToken(tok, out value)) return $"{what}: '{tok}' không phải số nguyên hợp lệ.";
        if (value < lo || value > hi) return $"{what} = {tok} nằm ngoài [{Util.LL(lo)}, {Util.LL(hi)}].";
        return null;
    }

    /// <summary>Sinh k giá trị đôi một khác nhau trong [lo, hi] (yêu cầu hi - lo + 1 &gt;= k).</summary>
    public static List<long> DistinctValues(Rng rng, long lo, long hi, int k)
    {
        var res = new List<long>(k);
        ulong span = Util.SpanOf(lo, hi);
        if (span != ulong.MaxValue && span + 1 <= 4UL * (ulong)k + 16)
        {
            // Miền nhỏ: xáo trộn một phần (Fisher-Yates) trên toàn bộ miền.
            var all = new long[(int)(span + 1)];
            for (int i = 0; i < all.Length; i++) all[i] = unchecked((long)((ulong)lo + (ulong)i));
            for (int i = 0; i < k; i++)
            {
                int j = i + (int)rng.Below((ulong)(all.Length - i));
                (all[i], all[j]) = (all[j], all[i]);
                res.Add(all[i]);
            }
            return res;
        }
        var seen = new HashSet<long>(k * 2);
        while (res.Count < k)
        {
            long v = rng.Range(lo, hi);
            if (seen.Add(v)) res.Add(v);
        }
        return res;
    }
}

/// <summary>Danh sách các dạng bài. Thêm module mới: tạo lớp con của Problem rồi thêm vào All.</summary>
public static class Problems
{
    public static IReadOnlyList<Problem> All { get; } = new Problem[]
    {
        new SingleNProblem(),
        new TwoIntsProblem(),
        new ArrayProblem(),
        new QueriesProblem(),
        new PrefixSumRangeProblem(),
        new TdcPrefixSumProblem(),
        new StringProblem(),
        new MatrixProblem(),
        new GridProblem(),
        new PairListProblem(),
        new GraphProblem(),
        new TreeProblem(),
        new MultiTestProblem(),
        new CustomProblem(),
        new ChessMovesProblem(),
        new ChessPieceMovesProblem(),
        new ChessKnightTargetsProblem(),
        new ChessKnightReachabilityProblem(),
        new ChessKnightShortestPathProblem(),
        // new MyProblem(),   // <- thêm module mới tại đây
    };

    public static Problem? Find(string id)
    {
        foreach (var p in All)
            if (p.Id == id) return p;
        return null;
    }
}
