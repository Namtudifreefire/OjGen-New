using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using OjGen.Core;

namespace OjGen.Gui;

public partial class SolutionMatrixWindow : Window
{
    private readonly string _testDirectory;
    private readonly Settings _settings;
    private readonly ObservableCollection<SolutionMatrixItem> _solutions = new();

    public SolutionMatrixWindow(string testDirectory, Settings settings)
    {
        InitializeComponent();
        _testDirectory = Path.GetFullPath(testDirectory);
        _settings = settings;
        lblFolder.Text = _testDirectory;
        gridSolutions.ItemsSource = _solutions;
        cmbReference.ItemsSource = _solutions;
        cmbRole.SelectedIndex = 0;
    }
    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn nhiều lời giải để so sánh",
            Multiselect = true,
            Filter = "Solution (*.c;*.cpp;*.cc;*.cxx;*.py;*.java;*.kt;*.pas;*.pp;*.cs;*.rs;*.go;*.exe;*.ojrun.json)|*.c;*.cpp;*.cc;*.cxx;*.py;*.java;*.kt;*.pas;*.pp;*.cs;*.rs;*.go;*.exe;*.ojrun.json|Tất cả file (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != true) return;
        foreach (string path in dialog.FileNames)
        {
            if (_solutions.Any(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
            var entry = new SolutionMatrixItem { Name = Path.GetFileNameWithoutExtension(path), Path = path, Role = _solutions.Count == 0 ? "Lời giải chuẩn" : "Ứng viên đúng" };
            _solutions.Add(entry);
            if (_solutions.Count == 1) cmbReference.SelectedItem = entry;
        }
    }
    private void BtnSetRole_Click(object sender, RoutedEventArgs e)
    {
        if (gridSolutions.SelectedItem is not SolutionMatrixItem selected || cmbRole.SelectedItem is not System.Windows.Controls.ComboBoxItem role) return;
        selected.Role = role.Content?.ToString() ?? "Ứng viên đúng";
        gridSolutions.Items.Refresh();
    }
    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        if (gridSolutions.SelectedItem is not SolutionMatrixItem selected) return;
        _solutions.Remove(selected);
        if (ReferenceEquals(cmbReference.SelectedItem, selected)) cmbReference.SelectedItem = _solutions.FirstOrDefault();
    }
    private async void BtnRun_Click(object sender, RoutedEventArgs e)
    {
        if (_solutions.Count < 2 || cmbReference.SelectedItem is not SolutionMatrixItem reference)
        {
            MessageBox.Show(this, "Thêm tối thiểu hai lời giải và chọn một lời giải chuẩn.", "Chưa đủ lời giải", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_solutions.Select(item => item.Name.Trim()).Any(name => name.Length == 0) || _solutions.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != _solutions.Count)
        {
            MessageBox.Show(this, "Mỗi lời giải cần tên riêng, không trùng.", "Tên không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var solutions = _solutions.Select(item => new SolutionMatrixItem { Name = item.Name.Trim(), Path = item.Path, Role = item.Role }).ToList();
        gridResults.ItemsSource = null;
        lblStatus.Text = $"Đang chạy {solutions.Count} lời giải trên bộ test có sẵn…";
        IsEnabled = false;
        try
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OjGen", "MatrixRuns");
            var report = await Task.Run(() => SolutionMatrixRunner.Run(_testDirectory, solutions, reference.Name.Trim(), _settings, root));
            gridResults.ItemsSource = report.Entries;
            lblStatus.Text = report.Error.Length > 0 ? report.Error :
                $"Đã so sánh {solutions.Count} lời giải trên {FileManager.ListFilesWithExt(_testDirectory, ".inp").Count} test. So sánh output chuẩn hóa theo token; bài có nhiều đáp án hợp lệ cần checker riêng.";
            if (report.Error.Length > 0) MessageBox.Show(this, report.Error, "Ma trận lời giải", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { IsEnabled = true; }
    }
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
