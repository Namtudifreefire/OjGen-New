namespace OjGen.Core;

/// <summary>Kiểm tra quân Mã có thể đi từ ô đầu đến ô đích trong đúng một nước không.</summary>
public sealed class ChessKnightReachabilityProblem : Problem
{
    public override string Id => "chess_knight_reachability";
    public override string Title => "Cờ vua: Mã đi được một nước giữa hai ô?";
    public override string MainCategory => "Dữ liệu tùy chỉnh";
    public override string FormatDescription =>
        "Bàn cờ trống 8×8, tọa độ (x,y) từ (0,0) đến (7,7).\n" +
        "Input gồm một dòng x1 y1 x2 y2, lần lượt là ô bắt đầu và ô đích của quân Mã.\n" +
        "Output là YES nếu Mã đi được từ ô đầu đến ô đích trong đúng một nước, ngược lại in NO.";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ChessBoardSupport.PositionParam(),
        ParamSpec.Int("targetMode", "Quan hệ giữa hai ô", 0, 0, 3,
            options: new[]
            {
                new ParamOption(0, "Ngẫu nhiên"),
                new ParamOption(1, "Đích đi được trong một nước"),
                new ParamOption(2, "Đích không đi được trong một nước"),
                new ParamOption(3, "Cùng một ô"),
            }),
    };

    public override string? ValidateConfig(Config c) => null;

    public override string Generate(Rng rng, Config c)
    {
        var (x1, y1) = ChessBoardSupport.PickPosition(rng, c.I("position"));
        var all = ChessBoardSupport.AllSquares().ToList();
        List<(int X, int Y)> candidates = c.I("targetMode") switch
        {
            1 => ChessBoardSupport.KnightTargets(x1, y1),
            2 => all.Where(p => !ChessBoardSupport.IsKnightMove(x1, y1, p.X, p.Y)).ToList(),
            3 => new List<(int X, int Y)> { (x1, y1) },
            _ => all,
        };
        var (x2, y2) = candidates[(int)rng.Below((ulong)candidates.Count)];
        return $"{x1} {y1} {x2} {y2}\n";
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (ChessBoardSupport.ParseCoordinates(input, 4, out var v) is { } error) return error;
        if (ChessBoardSupport.ValidatePositionMode(v[0], v[1], c.I("position")) is { } positionError) return positionError;
        bool isOneMove = ChessBoardSupport.IsKnightMove(v[0], v[1], v[2], v[3]);
        if (c.I("targetMode") == 1 && !isOneMove) return "Ô đích phải đi tới được bằng đúng một nước Mã.";
        if (c.I("targetMode") == 2 && isOneMove) return "Ô đích không được đi tới bằng một nước Mã.";
        if (c.I("targetMode") == 3 && (v[0] != v[2] || v[1] != v[3])) return "Ô đích phải trùng ô bắt đầu.";
        return null;
    }

    public override ulong EstimateMaxBytes(Config c) => 32;

    public override string DescribeConfig(Config c) =>
        $"  - Vùng ô bắt đầu: {c.I("position") switch { 1 => "góc", 2 => "cạnh, không ở góc", 3 => "trung tâm", _ => "bất kỳ" }}\n" +
        $"  - Quan hệ ô đích: {c.I("targetMode") switch { 1 => "đi được một nước", 2 => "không đi được một nước", 3 => "cùng ô", _ => "ngẫu nhiên" }}\n";
}
