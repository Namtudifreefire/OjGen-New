using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using OjGen.Core;

namespace OjGen.Gui;

public partial class ScaffoldWindow : Window
{
    private readonly IReadOnlyList<ScaffoldTemplate> _templates;
    private readonly Problem _problem;
    private readonly Config _config;

    public ScaffoldWindow(Problem problem, Config config)
    {
        InitializeComponent();
        _problem = problem;
        _config = config.Clone();
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

    private void BtnCreateContract_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Title = "Tạo bộ ca validator theo khuôn", Filter = "OjGen contract suite (*.ojcontract.json)|*.ojcontract.json", DefaultExt = ".ojcontract.json", AddExtension = true, FileName = _problem.Id + ".ojcontract.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            ValidatorContractRunner.CreateForProblem(_problem, _config).Save(dialog.FileName);
            lblHint.Text = $"Đã tạo {dialog.FileName} gồm input hợp lệ và các biến thể sai mà khuôn nhận diện được.";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Không tạo được contract", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void BtnTestValidator_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn validator cần chạy thử",
            Filter = "Validator (*.py;*.cpp;*.cc;*.c;*.cs;*.java;*.kt;*.pas;*.pp;*.rs;*.go;*.exe;*.ojrun.json)|*.py;*.cpp;*.cc;*.c;*.cs;*.java;*.kt;*.pas;*.pp;*.rs;*.go;*.exe;*.ojrun.json|Tất cả file (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var suite = ValidatorContractRunner.CreateForProblem(_problem, _config);
            IsEnabled = false;
            var settings = ((App)Application.Current).AppSettings;
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OjGen", "ContractRuns");
            var report = await Task.Run(() => ValidatorContractRunner.Run(dialog.FileName, suite, settings, root));
            string details = string.Join(Environment.NewLine, report.Cases.Select(item => (item.Passed ? "✓ " : "✗ ") + item.Name + ": " + item.Detail));
            if (suite.ValidatorCases.Count == 1) details += (details.Length > 0 ? Environment.NewLine : "") + "Khuôn này chỉ tạo được ca hợp lệ tự động; hãy bổ sung ca từ chối bằng tay để kiểm tra đầy đủ hơn.";
            if (report.Error.Length > 0) details = report.Error + (details.Length > 0 ? Environment.NewLine + details : "");
            MessageBox.Show(this, $"Validator: {report.Passed} đạt · {report.Failed} lỗi" + (details.Length > 0 ? Environment.NewLine + Environment.NewLine + details : ""),
                "Chạy thử validator", MessageBoxButton.OK, report.Failed == 0 && report.Error.Length == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Không chạy được validator", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { IsEnabled = true; }
    }
}
