// MatrixProblem.cs - Ma trận số nguyên hoặc hai mảng song song.
using System.Text;

namespace OjGen.Core;

public sealed class MatrixProblem : Problem
{
    private const long MaxCells = 20_000_000;

    public override string Id => "matrix";
    public override string Title => "Ma trận / hai mảng (doublearr)";
    public override string FormatDescription =>
        "Chọn một trong ba bố cục:\n" +
        "  0: dòng đầu n m, tiếp theo n dòng, mỗi dòng m số.\n" +
        "  1: dòng đầu n, m cố định trong cấu hình, tiếp theo n dòng.\n" +
        "  2: dòng đầu n, tiếp theo hai mảng, mỗi mảng nằm trên một dòng.\n" +
        "Mọi giá trị thuộc [minVal, maxVal].";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ParamSpec.Int("layout", "Bố cục dữ liệu", 0, 0, 2,
            options: new[]
            {
                new ParamOption(0, "Ma trận n × m (ghi n m)"),
                new ParamOption(1, "Ma trận, m cố định (chỉ ghi n)"),
                new ParamOption(2, "Hai mảng cùng độ dài n"),
            }),
        ParamSpec.Int("minN", "Số dòng / độ dài mảng nhỏ nhất", 1, 1, 1000000),
        ParamSpec.Int("maxN", "Số dòng / độ dài mảng lớn nhất", 10, 1, 1000000),
        ParamSpec.Int("minM", "Số cột nhỏ nhất", 1, 1, 1000000,
            visibleWhen: c => c.I("layout") != 2),
        ParamSpec.Int("maxM", "Số cột lớn nhất", 10, 1, 1000000,
            visibleWhen: c => c.I("layout") != 2),
        ParamSpec.Int("minVal", "Giá trị nhỏ nhất", 1, long.MinValue, long.MaxValue),
        ParamSpec.Int("maxVal", "Giá trị lớn nhất", 100, long.MinValue, long.MaxValue),
    };

    public override string? ValidateConfig(Config c)
    {
        if (c.I("minN") > c.I("maxN")) return "minN phải <= maxN.";
        if (c.I("minVal") > c.I("maxVal")) return "minVal phải <= maxVal.";
        long maxCols = c.I("layout") == 2 ? 2 : c.I("maxM");
        if (c.I("layout") != 2 && c.I("minM") > c.I("maxM")) return "minM phải <= maxM.";
        if (c.I("layout") == 1 && c.I("minM") != c.I("maxM"))
            return "Bố cục chỉ ghi n yêu cầu minM = maxM để m cố định.";
        if (c.I("maxN") > MaxCells / maxCols)
            return $"Ma trận vượt giới hạn {Util.LL(MaxCells)} ô.";
        return null;
    }

    public override string Generate(Rng rng, Config c)
    {
        int n = (int)rng.Range(c.I("minN"), c.I("maxN"));
        int layout = (int)c.I("layout");
        int m = layout == 2 ? n : (int)rng.Range(c.I("minM"), c.I("maxM"));
        int rows = layout == 2 ? 2 : n;
        var sb = new StringBuilder();
        Util.AppendLL(sb, n);
        if (layout == 0) { sb.Append(' '); Util.AppendLL(sb, m); }
        sb.Append('\n');
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < m; j++)
            {
                if (j > 0) sb.Append(' ');
                Util.AppendLL(sb, rng.Range(c.I("minVal"), c.I("maxVal")));
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (SplitInputLines(input, out var lines) is { } e) return e;
        int layout = (int)c.I("layout");
        int headerWidth = layout == 0 ? 2 : 1;
        if (lines[0].Length != headerWidth) return layout == 0 ? "Dòng 1 phải chứa n m." : "Dòng 1 phải chứa n.";
        if (ReadIntToken(lines[0][0], c.I("minN"), c.I("maxN"), "n", out long n) is { } e1) return e1;
        long m;
        if (layout == 0)
        {
            if (ReadIntToken(lines[0][1], c.I("minM"), c.I("maxM"), "m", out m) is { } e2) return e2;
        }
        else m = layout == 2 ? n : c.I("minM");
        long rowCount = layout == 2 ? 2 : n;
        if (lines.Count != rowCount + 1) return $"Số dòng phải là {Util.LL(rowCount + 1)}.";
        for (int i = 0; i < rowCount; i++)
        {
            var row = lines[i + 1];
            if (row.Length != m) return $"Dòng {i + 2} phải có đúng {Util.LL(m)} số.";
            for (int j = 0; j < row.Length; j++)
                if (ReadIntToken(row[j], c.I("minVal"), c.I("maxVal"), $"Ô [{i + 1},{j + 1}]", out _) is { } e3) return e3;
        }
        return null;
    }

    public override ulong EstimateMaxBytes(Config c)
    {
        ulong cols = (ulong)(c.I("layout") == 2 ? c.I("maxN") : c.I("maxM"));
        ulong rows = (ulong)(c.I("layout") == 2 ? 2 : c.I("maxN"));
        return rows * cols * (Util.MaxDigits(c.I("minVal"), c.I("maxVal")) + 1) + rows * 2 + 32;
    }
}
