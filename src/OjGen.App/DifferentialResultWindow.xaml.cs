using System.Text;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using OjGen.Core;

namespace OjGen.Gui;

public partial class DifferentialResultWindow : Window
{
    private readonly DifferentialResult _result;

    public DifferentialResultWindow(DifferentialResult result, ulong seed)
    {
        InitializeComponent();
        _result = result;
        lblTitle.Text = result.Ok ? "✓ Hai lời giải trùng kết quả" : result.Cancelled ? "Đã hủy kiểm thử đối chiếu" : "⚠ Tìm thấy khác biệt";
        lblTitle.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, result.Ok ? "SuccessBrush" : "WarningBrush");
        lblSummary.Text = $"Đã chạy {result.Tested} test · khác biệt/lỗi: {result.Mismatches} · seed: {Util.LL(seed)}" +
                          (result.Error.Length > 0 ? "\n" + result.Error : "");
        var sb = new StringBuilder();
        foreach (var failure in result.Failures)
        {
            sb.Append("=== Test ").Append(failure.TestIndex).Append(" ===\n").Append(failure.Reason).Append("\n");
            if (failure.Input.Length > 0) sb.Append("Input:\n").Append(ClipOutput(failure.Input)).Append('\n');
            if (failure.ReferenceOutput.Length > 0) sb.Append("Output chuẩn:\n").Append(ClipOutput(failure.ReferenceOutput)).Append('\n');
            if (failure.CandidateOutput.Length > 0) sb.Append("Output cần thử:\n").Append(ClipOutput(failure.CandidateOutput)).Append('\n');
            sb.Append('\n');
        }
        if (result.Failures.Count == 0 && result.Error.Length == 0 && result.Ok)
            sb.Append("Không phát hiện output khác nhau trên bộ test này. Điều đó không chứng minh hai lời giải đúng với mọi input.");
        if (result.Mismatches > result.Failures.Count)
            sb.Append("\n… Chỉ lưu tối đa 50 trường hợp đầu trong cửa sổ kết quả.");
        txtDetails.Text = sb.ToString();
        btnSaveFailure.IsEnabled = result.Failures.Count > 0;
    }

    private static string ClipOutput(string value, int limit = 12_000) =>
        value.Length <= limit ? value : value[..limit] + "\n… (đã cắt phần hiển thị)";

    private void BtnSaveFailure_Click(object sender, RoutedEventArgs e)
    {
        if (_result.Failures.Count == 0) return;
        var failure = _result.Failures[0];
        var dialog = new SaveFileDialog
        {
            Title = "Lưu input lỗi đầu tiên",
            Filter = "Input (*.inp)|*.inp",
            FileName = $"diff_test{failure.TestIndex:00}.inp",
            AddExtension = true,
            DefaultExt = ".inp",
        };
        if (dialog.ShowDialog(this) != true) return;
        string basePath = Path.Combine(Path.GetDirectoryName(dialog.FileName) ?? ".", Path.GetFileNameWithoutExtension(dialog.FileName));
        try
        {
            File.WriteAllText(dialog.FileName, failure.Input, new UTF8Encoding(false));
            File.WriteAllText(basePath + ".ref.out", failure.ReferenceOutput, new UTF8Encoding(false));
            File.WriteAllText(basePath + ".candidate.out", failure.CandidateOutput, new UTF8Encoding(false));
            File.WriteAllText(basePath + ".diff.txt", failure.Reason, new UTF8Encoding(false));
            MessageBox.Show(this, "Đã lưu input và hai output để tái hiện lỗi.", "OjGen", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Không lưu được test lỗi: " + ex.Message, "Lỗi lưu test", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
