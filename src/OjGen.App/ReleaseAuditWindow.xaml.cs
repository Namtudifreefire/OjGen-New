using System.Windows;

namespace OjGen.Gui;

public partial class ReleaseAuditWindow : Window
{
    internal ReleaseAuditWindow(ReleaseAuditReport report)
    {
        InitializeComponent();
        txtReport.Text = report.ToText();
        btnContinue.IsEnabled = !report.HasErrors && !report.Cancelled;
        if (report.Cancelled)
        {
            lblStatus.Text = "Đã hủy; chưa thể đóng gói.";
            lblStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "WarningBrush");
        }
        else if (report.HasErrors)
        {
            lblStatus.Text = $"Có {report.ErrorCount} lỗi cần sửa trước khi đóng gói.";
            lblStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "ErrorBrush");
        }
        else if (report.WarningCount > 0)
        {
            lblStatus.Text = $"Đạt có {report.WarningCount} lưu ý; bạn có thể tiếp tục sau khi rà lại.";
            lblStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "WarningBrush");
        }
        else
        {
            lblStatus.Text = "Tất cả bước kiểm định đều đạt.";
            lblStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "SuccessBrush");
        }
    }

    private void BtnContinue_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void BtnClose_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
