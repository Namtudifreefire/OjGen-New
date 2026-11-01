using System.IO;
using System.Windows;
using Microsoft.Win32;
using OjGen.Core;

namespace OjGen.Gui;

public partial class PackageWindow : Window
{
    private readonly string _testDirectory;
    internal ProblemPackageOptions? Result { get; private set; }

    internal PackageWindow(string name, string code, string parentDirectory, string testDirectory,
                           string solution, int sampleCount, int testCount, ProjectPackageOptions? previous = null)
    {
        InitializeComponent();
        _testDirectory = testDirectory;
        txtPackageName.Text = code.Length == 0 ? "problem" : code;
        txtParent.Text = parentDirectory;
        txtSolution.Text = solution;
        bool hasTests = Directory.Exists(testDirectory) && FileManager.ListFilesWithExt(testDirectory, ".inp").Count > 0;
        chkTests.IsChecked = hasTests;
        if (previous != null)
        {
            if (previous.PackageName.Length > 0) txtPackageName.Text = previous.PackageName;
            if (previous.ParentDirectory.Length > 0) txtParent.Text = previous.ParentDirectory;
            cmbExportFormat.SelectedIndex = previous.ExportFormat == "Icpc" ? 1 : 0;
            chkTests.IsChecked = hasTests && previous.IncludeTests;
            chkZip.IsChecked = previous.CreateZip;
            chkSolution.IsChecked = previous.IncludeReferenceSolution;
            if (previous.ReferenceSolution.Length > 0) txtSolution.Text = previous.ReferenceSolution;
            txtValidator.Text = previous.ValidatorPath;
            txtContractSuite.Text = previous.ContractSuitePath;
            txtChecker.Text = previous.CheckerPath;
            txtCheckerArguments.Text = previous.CheckerArguments;
            txtCheckerTimeout.Text = previous.CheckerTimeoutMs.ToString();
            txtCandidate.Text = previous.CandidateSolution;
        }
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

    private void BtnBrowseContracts_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Chọn contract suite", Filter = "OjGen contract suite (*.ojcontract.json)|*.ojcontract.json|JSON (*.json)|*.json", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) txtContractSuite.Text = dialog.FileName;
    }

    private void BtnCreateContractTemplate_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Title = "Tạo mẫu contract suite", Filter = "OjGen contract suite (*.ojcontract.json)|*.ojcontract.json", DefaultExt = ".ojcontract.json", AddExtension = true, FileName = "validation.ojcontract.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var suite = new ValidationContractSuite();
            var inputs = Directory.Exists(_testDirectory) ? FileManager.ListFilesWithExt(_testDirectory, ".inp") : new List<string>();
            if (inputs.Count > 0)
            {
                string input = FileManager.ReadText(inputs[0]);
                string answer = File.Exists(Path.ChangeExtension(inputs[0], ".out")) ? FileManager.ReadText(Path.ChangeExtension(inputs[0], ".out")) : "";
                suite.ValidatorCases.Add(new ValidatorContractCase { Name = "valid-sample", Input = input, ExpectedAccepted = true });
                suite.ValidatorCases.Add(new ValidatorContractCase { Name = "reject-empty", Input = "", ExpectedAccepted = false });
                if (answer.Length > 0)
                {
                    suite.CheckerCases.Add(new CheckerContractCase { Name = "accept-reference-answer", Input = input, ContestantOutput = answer, Answer = answer, ExpectedAccepted = true });
                    suite.CheckerCases.Add(new CheckerContractCase { Name = "reject-obviously-wrong", Input = input, ContestantOutput = "OJGEN_INVALID_OUTPUT\n", Answer = answer, ExpectedAccepted = false });
                }
            }
            suite.Save(dialog.FileName);
            txtContractSuite.Text = dialog.FileName;
        }
        catch (Exception ex) { lblError.Text = "Không tạo được contract suite: " + ex.Message; }
    }

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
        if (txtChecker.Text.Trim().Length > 0 && !CheckerAuditRunner.TryParseArguments(txtCheckerArguments.Text, out _, out string checkerArgsError)) { lblError.Text = checkerArgsError; return; }
        int checkerTimeoutMs = 0;
        if (!int.TryParse(txtCheckerTimeout.Text.Trim(), out checkerTimeoutMs) || checkerTimeoutMs < 0 || checkerTimeoutMs > 3600000)
        { lblError.Text = "Timeout checker phải là số nguyên từ 0 đến 3600000 ms."; return; }
        if (txtCandidate.Text.Trim().Length > 0 && !File.Exists(txtCandidate.Text.Trim().Trim('"'))) { lblError.Text = "Không tìm thấy lời giải độc lập."; return; }
        if (txtContractSuite.Text.Trim().Length > 0)
        {
            try { _ = ValidationContractSuite.Load(txtContractSuite.Text.Trim().Trim('"')); }
            catch (Exception ex) { lblError.Text = "Contract suite chưa hợp lệ: " + ex.Message; return; }
        }
        string exportFormat = cmbExportFormat.SelectedIndex == 1 ? "Icpc" : "OjGen";
        if (exportFormat == "Icpc" && chkTests.IsChecked != true)
        { lblError.Text = "Gói Kattis 2025-09 cần bật kèm test."; return; }
        if (exportFormat == "Icpc" && chkSolution.IsChecked != true)
        { lblError.Text = "Gói Kattis 2025-09 cần bật kèm lời giải chuẩn."; return; }
        if (exportFormat == "Icpc" && !string.Equals(Path.GetExtension(txtValidator.Text.Trim().Trim('"')), ".py", StringComparison.OrdinalIgnoreCase))
        { lblError.Text = "Gói Kattis 2025-09 cần chọn validator Python (.py)."; return; }
        Result = new ProblemPackageOptions
        {
            ExportFormat = exportFormat,
            ParentDirectory = txtParent.Text.Trim(),
            PackageName = txtPackageName.Text.Trim(),
            TestDirectory = _testDirectory,
            IncludeTests = chkTests.IsChecked == true,
            CreateZip = chkZip.IsChecked == true,
            ReferenceSolution = txtSolution.Text.Trim().Trim('"'),
            IncludeReferenceSolution = chkSolution.IsChecked == true,
            ValidatorPath = txtValidator.Text.Trim().Trim('"'),
            CheckerPath = txtChecker.Text.Trim().Trim('"'),
            CheckerArguments = txtCheckerArguments.Text.Trim(),
            CheckerTimeoutMs = checkerTimeoutMs,
            ContractSuitePath = txtContractSuite.Text.Trim().Trim('"'),
            CandidateSolution = txtCandidate.Text.Trim().Trim('"'),
        };
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
