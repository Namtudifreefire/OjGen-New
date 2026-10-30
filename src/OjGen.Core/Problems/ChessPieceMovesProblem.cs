namespace OjGen.Core;

/// <summary>Đếm số ô một quân cờ có thể đi tới trên bàn cờ trống 8×8.</summary>
public sealed class ChessPieceMovesProblem : Problem
{
    private static readonly string[] PieceNames = { "", "Mã", "Vua", "Xe", "Tượng", "Hậu" };

    public override string Id => "chess_piece_moves";
    public override string Title => "Cờ vua: đếm nước đi của quân cờ";
    public override string MainCategory => "Dữ liệu tùy chỉnh";
    public override string FormatDescription =>
        "Bàn cờ trống 8×8, tọa độ (x,y) từ (0,0) đến (7,7); không có quân cờ cản đường.\n" +
        "Input gồm một dòng x y. Quân cờ được chọn trong cấu hình: Mã, Vua, Xe, Tượng hoặc Hậu.\n" +
        "Output gồm một số nguyên: số ô quân cờ có thể đi tới theo luật cờ vua.";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ParamSpec.Int("piece", "Quân cờ", 1, 1, 5,
            options: new[]
            {
                new ParamOption(1, "Mã"),
                new ParamOption(2, "Vua"),
                new ParamOption(3, "Xe"),
                new ParamOption(4, "Tượng"),
                new ParamOption(5, "Hậu"),
            }),
        ChessBoardSupport.PositionParam(),
    };

    public override string? ValidateConfig(Config c) => null;

    public override string Generate(Rng rng, Config c)
    {
        var (x, y) = ChessBoardSupport.PickPosition(rng, c.I("position"));
        return $"{x} {y}\n";
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (ChessBoardSupport.ParseCoordinates(input, 2, out var v) is { } error) return error;
        return ChessBoardSupport.ValidatePositionMode(v[0], v[1], c.I("position"));
    }

    public override ulong EstimateMaxBytes(Config c) => 16;

    public override string DescribeConfig(Config c) =>
        $"  - Quân cờ: {PieceNames[(int)c.I("piece")]}\n" +
        $"  - Vùng tọa độ: {PositionName(c.I("position"))}\n";

    private static string PositionName(long mode) => mode switch
    {
        1 => "góc bàn cờ",
        2 => "cạnh bàn cờ, không ở góc",
        3 => "vùng trung tâm bàn cờ",
        _ => "bất kỳ",
    };
}
