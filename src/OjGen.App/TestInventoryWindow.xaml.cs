using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;
using OjGen.Core;

namespace OjGen.Gui;

public partial class TestInventoryWindow : Window
{
    private readonly string _directory;
    private readonly Problem? _problem;
    private readonly Config? _config;
    private readonly ObservableCollection<TestInventoryRow> _rows = new();
    private readonly ICollectionView _view;

    public TestInventoryWindow(string directory, Problem? problem = null, Config? config = null)
    {
        InitializeComponent();
        _directory = Path.GetFullPath(directory);
        _problem = problem;
        _config = config?.Clone();
        lblDirectory.Text = _directory;
        gridTests.ItemsSource = _rows;
        _view = CollectionViewSource.GetDefaultView(_rows);
        Reload();
    }

    private void Reload(bool validate = false)
    {
        try
        {
            var loaded = validate && _problem != null && _config != null
                ? TestInventoryService.Validate(_directory, _problem, _config)
                : TestInventoryService.Read(_directory);
            _rows.Clear();
            foreach (var row in loaded) _rows.Add(row);
            _view.Refresh();
            int missing = _rows.Count(row => row.OutputBytes == null);
            int duplicates = _rows.Count(row => row.DuplicateOf.Length > 0);
            int invalid = _rows.Count(row => row.Validation is not "Chưa chạy" and not "Hợp lệ");
            lblSummary.Text = $"{_rows.Count} input · {missing} thiếu output · {duplicates} input trùng · {invalid} lỗi kiểm định";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Không đọc được bộ test", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private List<TestInventoryRow> SelectedRows() => gridTests.SelectedItems.OfType<TestInventoryRow>().ToList();
    private void BtnReload_Click(object sender, RoutedEventArgs e) => Reload();
    private void TxtFilter_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_view == null) return;
        string query = txtFilter.Text.Trim();
        _view.Filter = item => item is TestInventoryRow row && (query.Length == 0 ||
            (row.Name + " " + row.Group + " " + row.Tags + " " + row.Validation).Contains(query, StringComparison.OrdinalIgnoreCase));
    }
    private void BtnRename_Click(object sender, RoutedEventArgs e)
    {
        try { TestInventoryService.Rename(_directory, SelectedRows(), txtRenamePrefix.Text); Reload(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Không đổi được tên test", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void BtnAssignGroup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (TestCoverageTags.Parse(txtTags.Text, out var tags) is { } error) throw new InvalidDataException(error);
            TestInventoryService.AssignGroup(_directory, SelectedRows(), txtGroup.Text, tags);
            Reload();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Không cập nhật được nhóm", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void BtnVisualize_Click(object sender, RoutedEventArgs e)
    {
        var row = SelectedRows().FirstOrDefault();
        if (row == null) { MessageBox.Show(this, "Chọn một test trước.", "Chưa chọn test", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        new TestVisualizationWindow(row.InputPath) { Owner = this }.ShowDialog();
    }
    private void BtnValidate_Click(object sender, RoutedEventArgs e)
    {
        if (_problem == null || _config == null) { MessageBox.Show(this, "Mở kiểm kê từ cửa sổ chính để dùng đúng khuôn và cấu hình hiện tại.", "Thiếu khuôn bài", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        Reload(validate: true);
    }
    private async void BtnRunChecker_Click(object sender, RoutedEventArgs e)
    {
        var inputs = _rows.Select(row => row.InputPath).Where(File.Exists).ToList();
        if (inputs.Count == 0) { MessageBox.Show(this, "Không có input để chạy checker.", "Bộ test rỗng", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var dialog = new OpenFileDialog
        {
            Title = "Chọn checker để chạy lại trên output đang có",
            Filter = "Checker (*.cpp;*.cc;*.cxx;*.py;*.java;*.cs;*.rs;*.go;*.exe;*.ojrun.json)|*.cpp;*.cc;*.cxx;*.py;*.java;*.cs;*.rs;*.go;*.exe;*.ojrun.json|Tất cả file (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true) return;
        var settings = ((App)Application.Current).AppSettings;
        string args = txtCheckerArgs.Text.Trim();
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OjGen", "ExistingCheckerRuns");
        lblSummary.Text = $"Đang chạy checker trên {inputs.Count} test có sẵn…";
        IsEnabled = false;
        CheckerAuditResult report;
        try
        {
            report = await Task.Run(() => CheckerAuditRunner.Run(dialog.FileName, args, inputs, settings, root, NullLog.Instance));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Không chạy được checker", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        finally { IsEnabled = true; }
        string details = report.TestDetails;
        if (report.Error.Length > 0) details = report.Error + (details.Length > 0 ? Environment.NewLine + details : "");
        if (report.SelfTestWarning.Length > 0) details += (details.Length > 0 ? Environment.NewLine + Environment.NewLine : "") + report.SelfTestWarning;
        lblSummary.Text = $"Checker: {report.Passed} đạt · {report.Failed} lỗi";
        MessageBox.Show(this, $"Checker: {report.Passed} đạt · {report.Failed} lỗi" +
            (details.Length > 0 ? Environment.NewLine + Environment.NewLine + details : ""),
            "Kiểm tra lại bộ test", MessageBoxButton.OK, report.Ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }
    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Title = "Xuất kiểm kê test", Filter = "CSV (*.csv)|*.csv", DefaultExt = ".csv", AddExtension = true, FileName = "TEST_INVENTORY.csv" };
        if (dialog.ShowDialog(this) != true) return;
        try { TestInventoryService.ExportCsv(dialog.FileName, _rows.ToList()); lblSummary.Text = "Đã xuất " + dialog.FileName; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Không xuất được CSV", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
