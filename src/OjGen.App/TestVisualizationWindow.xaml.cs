using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OjGen.Gui;

public partial class TestVisualizationWindow : Window
{
    private readonly string _inputPath;
    private string[] _lines = Array.Empty<string>();
    public TestVisualizationWindow(string inputPath)
    {
        InitializeComponent();
        _inputPath = inputPath;
        Title = "Trực quan hóa · " + System.IO.Path.GetFileName(inputPath);
        lblFile.Text = System.IO.Path.GetFileName(inputPath);
        txtInput.Text = File.ReadAllText(inputPath);
        _lines = txtInput.Text.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        cmbView.SelectedIndex = 0;
        Loaded += (_, _) => Render();
        SizeChanged += (_, _) => Render();
    }
    private void CmbView_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (IsLoaded) Render(); }
    private string SelectedMode() => ((cmbView.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tự nhận diện");
    private static string[][] Tokens(string[] lines) => lines.Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToArray();
    private void Render()
    {
        canvas.Children.Clear();
        if (_lines.Length == 0) { lblSummary.Text = "Input rỗng."; return; }
        var tokenLines = Tokens(_lines);
        string mode = SelectedMode();
        if (mode == "Tự nhận diện") mode = Detect(tokenLines);
        if (mode == "Đồ thị / cây" && DrawGraph(tokenLines)) return;
        if (mode == "Lưới" && DrawGrid(tokenLines)) return;
        DrawDistribution();
    }
    private string Detect(string[][] tokens)
    {
        if (tokens[0].Length >= 2 && int.TryParse(tokens[0][0], out int a) && int.TryParse(tokens[0][1], out int b) && a is > 0 and <= 500 && b is >= 0 and <= 10000)
        {
            if (tokens.Length > 1 && tokens.Skip(1).Take(Math.Min(b, tokens.Length - 1)).All(row => row.Length == 2 && row.All(token => int.TryParse(token, out _)))) return "Đồ thị / cây";
            if (tokens.Length >= a + 1 && tokens.Skip(1).Take(a).All(row => row.Length == b && row.All(token => token.Length == 1))) return "Lưới";
        }
        if (tokens[0].Length == 1 && int.TryParse(tokens[0][0], out int treeNodes) && treeNodes is > 1 and <= 1000 &&
            tokens.Length >= treeNodes && tokens.Skip(1).Take(treeNodes - 1).All(row => row.Length >= 2 && row.Take(2).All(token => int.TryParse(token, out _))))
            return "Đồ thị / cây";
        return "Phân phối số";
    }
    private bool DrawGraph(string[][] rows)
    {
        if (rows[0].Length < 1 || !int.TryParse(rows[0][0], out int n) || n < 1 || n > 1000) return false;
        int m;
        if (rows[0].Length >= 2)
        {
            if (!int.TryParse(rows[0][1], out m) || m < 0 || m > 10000) return false;
        }
        else m = n - 1;
        var edges = new List<(int U, int V)>();
        foreach (var row in rows.Skip(1))
        {
            if (row.Length < 2 || !int.TryParse(row[0], out int u) || !int.TryParse(row[1], out int v)) break;
            if (u < 1 || u > n || v < 1 || v > n) return false;
            edges.Add((u - 1, v - 1));
            if (edges.Count >= m) break;
        }
        if (edges.Count == 0 && m > 0) return false;
        double width = Math.Max(200, canvas.ActualWidth), height = Math.Max(200, canvas.ActualHeight);
        double radius = Math.Max(30, Math.Min(width, height) * .39), cx = width / 2, cy = height / 2, nodeR = Math.Clamp(180.0 / Math.Sqrt(n), 7, 16);
        var points = Enumerable.Range(0, n).Select(i => (X: cx + radius * Math.Cos(2 * Math.PI * i / n - Math.PI / 2), Y: cy + radius * Math.Sin(2 * Math.PI * i / n - Math.PI / 2))).ToArray();
        foreach (var (u, v) in edges)
        {
            canvas.Children.Add(new Line { X1 = points[u].X, Y1 = points[u].Y, X2 = points[v].X, Y2 = points[v].Y,
                Stroke = (Brush)FindResource("TextSecondaryBrush"), StrokeThickness = 1.2, Opacity = .7 });
        }
        for (int i = 0; i < n; i++)
        {
            var ellipse = new Ellipse { Width = nodeR * 2, Height = nodeR * 2, Fill = (Brush)FindResource("AccentBrush"), Stroke = Brushes.White, StrokeThickness = 1 };
            Canvas.SetLeft(ellipse, points[i].X - nodeR); Canvas.SetTop(ellipse, points[i].Y - nodeR); canvas.Children.Add(ellipse);
        }
        lblSummary.Text = $"Đồ thị/cây: {n} đỉnh · {edges.Count} cạnh. Vị trí đỉnh là bố trí trực quan, không biểu diễn tọa độ của đề.";
        return true;
    }
    private bool DrawGrid(string[][] rows)
    {
        if (rows[0].Length < 2 || !int.TryParse(rows[0][0], out int n) || !int.TryParse(rows[0][1], out int m) || n is < 1 or > 200 || m is < 1 or > 200 || rows.Length < n + 1) return false;
        var cells = rows.Skip(1).Take(n).Select(row => string.Concat(row)).ToArray();
        if (cells.Any(row => row.Length != m)) return false;
        double cell = Math.Max(2, Math.Min((canvas.ActualWidth - 16) / m, (canvas.ActualHeight - 16) / n));
        for (int y = 0; y < n; y++) for (int x = 0; x < m; x++)
        {
            var rect = new Rectangle { Width = cell, Height = cell, Fill = cells[y][x] is '.' or '0' ? Brushes.WhiteSmoke : (Brush)FindResource("AccentBrush"), Stroke = Brushes.Gray, StrokeThickness = .4 };
            Canvas.SetLeft(rect, x * cell); Canvas.SetTop(rect, y * cell); canvas.Children.Add(rect);
        }
        lblSummary.Text = $"Lưới {n} × {m} · {n * m} ô. Ô '.' hoặc '0' hiển thị sáng; ký tự khác hiển thị theo màu nhấn.";
        return true;
    }
    private void DrawDistribution()
    {
        var values = System.Text.RegularExpressions.Regex.Matches(txtInput.Text, @"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")
            .Select(match => double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? (double?)value : null)
            .Where(value => value.HasValue).Select(value => value!.Value).Take(50000).ToList();
        if (values.Count == 0) { lblSummary.Text = "Không tìm thấy giá trị số để vẽ phân phối."; return; }
        double min = values.Min(), max = values.Max(), width = Math.Max(100, canvas.ActualWidth), height = Math.Max(100, canvas.ActualHeight);
        int[] bins = new int[20];
        foreach (double value in values) bins[max == min ? 0 : Math.Clamp((int)((value - min) / (max - min) * bins.Length), 0, bins.Length - 1)]++;
        int peak = Math.Max(1, bins.Max());
        for (int i = 0; i < bins.Length; i++)
        {
            double barWidth = width / bins.Length - 3, barHeight = (height - 34) * bins[i] / peak;
            var rect = new Rectangle { Width = Math.Max(1, barWidth), Height = barHeight, Fill = (Brush)FindResource("AccentBrush") };
            Canvas.SetLeft(rect, i * width / bins.Length); Canvas.SetTop(rect, height - barHeight - 24); canvas.Children.Add(rect);
        }
        lblSummary.Text = $"Phân phối {values.Count:N0} giá trị số · min {min.ToString("G6", CultureInfo.InvariantCulture)} · max {max.ToString("G6", CultureInfo.InvariantCulture)} · 20 khoảng.";
    }
}
