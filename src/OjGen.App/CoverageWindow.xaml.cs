using System.Windows;
using OjGen.Core;

namespace OjGen.Gui;

public partial class CoverageWindow : Window
{
    public CoverageWindow(CoverageReport report)
    {
        InitializeComponent();
        lblSummary.Text = report.Summary;
        txtReport.Text = report.ToText();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
