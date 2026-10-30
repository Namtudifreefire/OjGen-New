// ChessMovesProblem.cs - sinh vị trí quân cờ trên bàn cờ 8x8 trống.
namespace OjGen.Core;

public sealed class ChessMovesProblem : Problem
{
    private const int BoardSize = 8;

    private static readonly (int Row, int Col)[] Corners =
    {
        (0, 0), (0, BoardSize - 1), (BoardSize - 1, 0), (BoardSize - 1, BoardSize - 1),
    };

    private static readonly (int Row, int Col)[] Edges =
        Enumerable.Range(1, BoardSize - 2)
            .SelectMany(i => new[]
            {
                (0, i), (BoardSize - 1, i), (i, 0), (i, BoardSize - 1),
            })
            .ToArray();

    public override string Id => "chess_moves";
    public override string Title => "Cờ vua: liệt kê ô quân có thể đi";
    public override string MainCategory => "Dữ liệu tùy chỉnh";

    public override string FormatDescription =>
        "Bàn cờ trống 8×8, tọa độ (hàng, cột) đánh số từ 0 đến 7. Mỗi test có một quân cờ.\n" +
        "Dòng 1: mã_quân hàng cột màu. Mã quân: K (Vua), Q (Hậu), R (Xe), B (Tượng), N (Mã), P (Tốt); màu: W (Trắng) hoặc B (Đen).\n" +
        "Mã đi theo luật cờ vua chuẩn: (1,2)/(2,1). Chỉ xét ô đích trong bàn cờ; không có quân cản, không xét chiếu, nhập thành, bắt tốt qua đường hoặc phong cấp. Tốt chỉ đi thẳng; Trắng đi về hàng giảm, Đen đi về hàng tăng; được đi hai ô từ hàng xuất phát nếu đường trống.\n" +
        "Ví dụ: N 0 0 W là Mã ở (0,0), đi được 2 ô: (1,2) và (2,1).\n" +
        "Bài mẫu có thể in số ô đi được ở dòng đầu, sau đó in mỗi tọa độ đích trên một dòng. File .out được chạy từ lời giải chuẩn đã chọn.";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ParamSpec.Int("piece", "Quân cờ (0 = ngẫu nhiên)", 5, 0, 6,
            options: new[]
            {
                new ParamOption(0, "Ngẫu nhiên giữa 6 quân"),
                new ParamOption(1, "Vua (K)"),
                new ParamOption(2, "Hậu (Q)"),
                new ParamOption(3, "Xe (R)"),
                new ParamOption(4, "Tượng (B)"),
                new ParamOption(5, "Mã (N)"),
                new ParamOption(6, "Tốt (P)"),
            }),
        ParamSpec.Int("position", "Vị trí (0 = bất kỳ)", 0, 0, 3,
            options: new[]
            {
                new ParamOption(0, "Bất kỳ"),
                new ParamOption(1, "Góc bàn cờ"),
                new ParamOption(2, "Cạnh bàn cờ (không ở góc)"),
                new ParamOption(3, "Vùng trung tâm (hàng/cột 2–5)"),
            }),
        ParamSpec.Int("side", "Màu quân Tốt (0 = ngẫu nhiên)", 0, 0, 2,
            options: new[]
            {
                new ParamOption(0, "Ngẫu nhiên"),
                new ParamOption(1, "Trắng (W)"),
                new ParamOption(2, "Đen (B)"),
            },
            visibleWhen: c => c.I("piece") is 0 or 6),
    };

    public override string? ValidateConfig(Config c) => null;

    public override string Generate(Rng rng, Config c)
    {
        int piece = (int)c.I("piece");
        if (piece == 0) piece = (int)rng.Range(1, 6);
        int side = (int)c.I("side");
        if (side == 0) side = (int)rng.Range(1, 2);
        var (row, col) = PickPosition(rng, (int)c.I("position"));

        return $"{PieceCode(piece)} {row} {col} {(side == 1 ? 'W' : 'B')}\n";
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (SplitInputLines(input, out var lines) is { } error) return error;
        if (lines.Count != 1 || lines[0].Length != 4)
            return "Input phải có đúng một dòng gồm mã quân, hàng, cột và màu.";

        string pieceToken = lines[0][0];
        int piece = pieceToken.Length == 1 ? PieceType(char.ToUpperInvariant(pieceToken[0])) : 0;
        if (piece == 0)
            return "Mã quân phải là K, Q, R, B, N hoặc P.";
        if (c.I("piece") != 0 && piece != c.I("piece"))
            return "Mã quân không khớp với lựa chọn quân cờ.";

        if (ReadIntToken(lines[0][1], 0, BoardSize - 1, "Hàng", out long row) is { } rowError) return rowError;
        if (ReadIntToken(lines[0][2], 0, BoardSize - 1, "Cột", out long col) is { } colError) return colError;

        string sideToken = lines[0][3].ToUpperInvariant();
        if (sideToken is not ("W" or "B")) return "Màu quân phải là W (Trắng) hoặc B (Đen).";
        if (piece == 6 && c.I("side") != 0 &&
            sideToken != (c.I("side") == 1 ? "W" : "B"))
            return "Màu Tốt không khớp với lựa chọn trong cấu hình.";

        bool onEdge = row == 0 || row == BoardSize - 1 || col == 0 || col == BoardSize - 1;
        bool corner = (row == 0 || row == BoardSize - 1) && (col == 0 || col == BoardSize - 1);
        if (c.I("position") == 1 && !corner) return "Vị trí phải ở một góc bàn cờ.";
        if (c.I("position") == 2 && (!onEdge || corner)) return "Vị trí phải ở cạnh bàn cờ, không phải góc.";
        if (c.I("position") == 3 && (row < 2 || row > 5 || col < 2 || col > 5))
            return "Vị trí phải ở vùng trung tâm bàn cờ (hàng và cột từ 2 đến 5).";

        return null;
    }

    public override ulong EstimateMaxBytes(Config c) => 16;

    public override string DescribeConfig(Config c)
    {
        string piece = c.I("piece") == 0 ? "ngẫu nhiên" : PieceLabel((int)c.I("piece"));
        string position = c.I("position") switch
        {
            1 => "góc bàn cờ",
            2 => "cạnh bàn cờ, không ở góc",
            3 => "vùng bên trong bàn cờ",
            _ => "bất kỳ",
        };
        string side = c.I("side") switch
        {
            1 => "Trắng",
            2 => "Đen",
            _ => "ngẫu nhiên",
        };
        return $"  - Bàn cờ: {BoardSize}×{BoardSize}, tọa độ 0..{BoardSize - 1}\n" +
               $"  - Quân cờ: {piece}\n" +
               $"  - Màu Tốt: {side}\n" +
               $"  - Vị trí: {position}\n";
    }

    private static (int Row, int Col) PickPosition(Rng rng, int mode) => mode switch
    {
        1 => Corners[(int)rng.Below((ulong)Corners.Length)],
        2 => Edges[(int)rng.Below((ulong)Edges.Length)],
        3 => ((int)rng.Range(2, 5), (int)rng.Range(2, 5)),
        _ => ((int)rng.Range(0, BoardSize - 1), (int)rng.Range(0, BoardSize - 1)),
    };

    private static char PieceCode(int piece) => piece switch
    {
        1 => 'K',
        2 => 'Q',
        3 => 'R',
        4 => 'B',
        5 => 'N',
        6 => 'P',
        _ => throw new ArgumentOutOfRangeException(nameof(piece)),
    };

    private static int PieceType(char piece) => char.ToUpperInvariant(piece) switch
    {
        'K' => 1,
        'Q' => 2,
        'R' => 3,
        'B' => 4,
        'N' => 5,
        'P' => 6,
        _ => 0,
    };

    private static string PieceLabel(int piece) => piece switch
    {
        1 => "Vua",
        2 => "Hậu",
        3 => "Xe",
        4 => "Tượng",
        5 => "Mã",
        6 => "Tốt",
        _ => "không xác định",
    };
}
