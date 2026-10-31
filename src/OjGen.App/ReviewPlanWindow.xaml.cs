using System.Windows;

namespace OjGen.Gui;

public partial class ReviewPlanWindow : Window
{
    public ReviewPlanWindow(string summary, string samples, bool valid, string? error)
    {
        InitializeComponent();
        lblSummary.Text = summary;
        txtSamples.Text = samples;
        lblValidation.Text = valid
            ? "✓ Input mẫu của tất cả nhóm đã qua kiểm tra định dạng và ràng buộc."
            : "✗ Không thể tiếp tục: " + (error ?? "input mẫu không hợp lệ.");
        lblValidation.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, valid ? "SuccessBrush" : "ErrorBrush");
        btnCreate.IsEnabled = valid;
    }

    private void BtnCreate_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
