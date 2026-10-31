using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using OjGen.Core;

namespace OjGen.Gui;

public partial class ScaffoldWindow : Window
{
    private readonly IReadOnlyList<ScaffoldTemplate> _templates;

    public ScaffoldWindow(Problem problem, Config config)
    {
        InitializeComponent();
        _templates = AuthoringScaffolds.Build(problem, config);
        cmbTemplate.ItemsSource = _templates;
        cmbTemplate.SelectedIndex = 0;
    }

    private void CmbTemplate_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (cmbTemplate.SelectedItem is not ScaffoldTemplate template) return;
        txtSource.Text = template.Source;
        lblHint.Text = template.Readme;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (cmbTemplate.SelectedItem is not ScaffoldTemplate template) return;
        string ext = Path.GetExtension(template.FileName);
        var dialog = new SaveFileDialog
        {
            Title = "Lưu khung mã nguồn",
            Filter = $"Source (*{ext})|*{ext}|Tất cả file (*.*)|*.*",
            FileName = template.FileName,
            DefaultExt = ext,
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != true) return;
        string readme = Path.ChangeExtension(dialog.FileName, ".README.md");
        if (File.Exists(readme) && MessageBox.Show(this, "File hướng dẫn kèm theo đã tồn tại. Ghi đè?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;
        try
        {
            File.WriteAllText(dialog.FileName, template.Source, new UTF8Encoding(false));
            File.WriteAllText(readme, template.Readme + Environment.NewLine, new UTF8Encoding(false));
            MessageBox.Show(this, "Đã lưu khung mã và README. Hoàn thành TODO trước khi dùng validator/checker.", "OjGen", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Không lưu được khung mã: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
