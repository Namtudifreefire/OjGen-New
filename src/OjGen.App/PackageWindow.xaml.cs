using System.IO;
using System.Windows;
using Microsoft.Win32;
using OjGen.Core;

namespace OjGen.Gui;

public partial class PackageWindow : Window
{
    private readonly string _testDirectory;
    internal ProblemPackageOptions? Result { get; private set; }

    public PackageWindow(string name, string code, string parentDirectory, string testDirectory,
                         string solution, int sampleCount, int testCount)
    {
        InitializeComponent();
        _testDirectory = testDirectory;
        txtPackageName.Text = code.Length == 0 ? "problem" : code;
        txtParent.Text = parentDirectory;
        txtSolution.Text = solution;
        bool hasTests = Directory.Exists(testDirectory) && FileManager.ListFilesWithExt(testDirectory, ".inp").Count > 0;
        chkTests.IsChecked = hasTests;
        chkTests.IsEnabled = hasTests;
        lblTestsHint.Text = hasTests
            ? $"Nguồn: {testDirectory} · {testCount} test đã tạo"
            : "Chưa có thư mục test hoàn chỉnh; có thể đóng gói statement và sample hiện có.";
        lblSummary.Text = $"{name} [{(code.Length == 0 ? "chưa đặt mã" : code)}] · {sampleCount} sample được đánh dấu.";
    }

    private void BrowseInto(System.Windows.Controls.TextBox target, string title)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = "Source / solution (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) target.Text = dialog.FileName;
    }

    private void BtnBrowseParent_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Chọn thư mục cha để lưu gói" };
        if (dialog.ShowDialog(this) == true) txtParent.Text = dialog.FolderName;
    }

    private void BtnBrowseSolution_Click(object sender, RoutedEventArgs e) => BrowseInto(txtSolution, "Chọn lời giải chuẩn");
    private void BtnBrowseValidator_Click(object sender, RoutedEventArgs e) => BrowseInto(txtValidator, "Chọn validator");
    private void BtnBrowseChecker_Click(object sender, RoutedEventArgs e) => BrowseInto(txtChecker, "Chọn checker");
    private void BtnBrowseCandidate_Click(object sender, RoutedEventArgs e) => BrowseInto(txtCandidate, "Chọn lời giải độc lập để đối chiếu");

    private void BtnCreate_Click(object sender, RoutedEventArgs e)
    {
        if (txtPackageName.Text.Trim().Length == 0) { lblError.Text = "Tên gói không được để trống."; return; }
        if (txtParent.Text.Trim().Length == 0) { lblError.Text = "Chọn thư mục cha để lưu gói."; return; }
        try { Directory.CreateDirectory(txtParent.Text.Trim()); }
        catch (Exception ex) { lblError.Text = "Không tạo được thư mục cha: " + ex.Message; return; }
        if (chkTests.IsChecked == true && !Directory.Exists(_testDirectory)) { lblError.Text = "Thư mục test không còn tồn tại."; return; }
        if (chkSolution.IsChecked == true && !File.Exists(txtSolution.Text.Trim().Trim('"'))) { lblError.Text = "Không tìm thấy lời giải chuẩn."; return; }
        if (txtValidator.Text.Trim().Length > 0 && !File.Exists(txtValidator.Text.Trim().Trim('"'))) { lblError.Text = "Không tìm thấy validator."; return; }
        if (txtChecker.Text.Trim().Length > 0 && !File.Exists(txtChecker.Text.Trim().Trim('"'))) { lblError.Text = "Không tìm thấy checker."; return; }
        if (txtCandidate.Text.Trim().Length > 0 && !File.Exists(txtCandidate.Text.Trim().Trim('"'))) { lblError.Text = "Không tìm thấy lời giải độc lập."; return; }
        Result = new ProblemPackageOptions
        {
            ParentDirectory = txtParent.Text.Trim(),
            PackageName = txtPackageName.Text.Trim(),
            TestDirectory = _testDirectory,
            IncludeTests = chkTests.IsChecked == true,
            CreateZip = chkZip.IsChecked == true,
            ReferenceSolution = txtSolution.Text.Trim().Trim('"'),
            IncludeReferenceSolution = chkSolution.IsChecked == true,
            ValidatorPath = txtValidator.Text.Trim().Trim('"'),
            CheckerPath = txtChecker.Text.Trim().Trim('"'),
            CandidateSolution = txtCandidate.Text.Trim().Trim('"'),
        };
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
