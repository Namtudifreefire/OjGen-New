using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using OjGen.Core;

namespace OjGen.Gui;

public partial class StatementEditorWindow : Window
{
    private readonly Problem _problem;
    private readonly Config _config;
    private readonly List<ManualTestSpec> _samples;
    private readonly DispatcherTimer _previewTimer;
    private bool _loading;

    internal ProblemAuthoringDocument Document { get; private set; }
    internal ProblemAuthoringDocument? Result { get; private set; }

    internal StatementEditorWindow(Problem problem, Config config, string name, string code,
                                 IReadOnlyList<ManualTestSpec> samples, ProblemAuthoringDocument? existing)
    {
        InitializeComponent();
        _problem = problem;
        _config = config.Clone();
        _samples = samples.Where(sample => sample.IsSample).Select(sample => sample.Clone()).ToList();
        Document = existing != null && existing.ProblemId == problem.Id
            ? existing.Clone()
            : ProblemAuthoringDocument.Create(problem, config, name, code);
        Document.Name = name.Length == 0 ? Document.Name : name;
        Document.Code = code.Length == 0 ? Document.Code : code;
        if (_samples.Count > 0 && Document.Examples.StartsWith("<!--")) Document.Examples = BuildExamples();

        _loading = true;
        txtName.Text = Document.Name;
        txtCode.Text = Document.Code;
        txtTime.Text = Document.TimeLimitMs.ToString();
        txtMemory.Text = Document.MemoryLimitMb.ToString();
        txtDescription.Text = Document.Description;
        txtInput.Text = Document.Input;
        txtOutput.Text = Document.Output;
        txtConstraints.Text = Document.Constraints;
        txtExamples.Text = Document.Examples;
        _loading = false;

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            UpdateDocument();
            browserPreview.NavigateToString(Document.ToHtml());
        };
        Loaded += (_, _) => browserPreview.NavigateToString(Document.ToHtml());
    }

    private string BuildExamples()
    {
        var sb = new StringBuilder();
        int number = 0;
        foreach (var sample in _samples)
        {
            number++;
            string output = sample.ExpectedOutput.Length > 0 ? sample.ExpectedOutput : sample.GeneratedOutput;
            sb.Append("### Ví dụ ").Append(number).Append("\n\nInput\n```text\n")
                .Append(sample.Input.TrimEnd('\r', '\n')).Append("\n```\n\nOutput\n```text\n")
                .Append(output.TrimEnd('\r', '\n')).Append("\n```\n\n");
        }
        return sb.ToString().TrimEnd();
    }

    private void UpdateDocument()
    {
        Document.Name = txtName.Text.Trim();
        Document.Code = txtCode.Text.Trim();
        Document.TimeLimitMs = int.TryParse(txtTime.Text, out int time) ? time : 0;
        Document.MemoryLimitMb = int.TryParse(txtMemory.Text, out int memory) ? memory : 0;
        Document.Description = txtDescription.Text;
        Document.Input = txtInput.Text;
        Document.Output = txtOutput.Text;
        Document.Constraints = txtConstraints.Text;
        Document.Examples = txtExamples.Text;
    }

    private void Document_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_loading) return;
        UpdateDocument();
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void Document_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loading || !IsLoaded) return;
        UpdateDocument();
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void BtnInsertSamples_Click(object sender, RoutedEventArgs e)
    {
        if (_samples.Count == 0)
        {
            MessageBox.Show(this, "Chưa có test nào được đánh dấu Sample. Hãy thêm trong nút Test tay/mẫu trước.", "Chưa có sample", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        txtExamples.Text = BuildExamples();
    }

    private StatementCheckResult CheckDocument()
    {
        UpdateDocument();
        return StatementConsistencyChecker.Check(Document, _problem, _config, _samples);
    }

    private void BtnCheck_Click(object sender, RoutedEventArgs e)
    {
        var check = CheckDocument();
        lblCheckStatus.Text = check.Errors.Count == 0 ? "Đã kiểm tra bản đề; xem các lưu ý nếu có." : $"Có {check.Errors.Count} lỗi cần sửa.";
        lblCheckStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, check.Errors.Count == 0 ? "SuccessBrush" : "ErrorBrush");
        MessageBox.Show(this, StatementConsistencyChecker.Format(check), "Kiểm tra tính nhất quán", MessageBoxButton.OK,
            check.Errors.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void BtnSaveDraft_Click(object sender, RoutedEventArgs e)
    {
        UpdateDocument();
        var dialog = new SaveFileDialog { Title = "Lưu bản nháp đề", Filter = "OjGen draft (*.ojdraft.json)|*.ojdraft.json|JSON (*.json)|*.json", FileName = (Document.Code.Length == 0 ? "problem" : Document.Code) + ".ojdraft.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { Document.Save(dialog.FileName); lblCheckStatus.Text = "Đã lưu bản nháp: " + dialog.FileName; }
        catch (Exception ex) { MessageBox.Show(this, "Không lưu được bản nháp: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void BtnLoadDraft_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Mở bản nháp đề", Filter = "OjGen draft (*.ojdraft.json;*.json)|*.ojdraft.json;*.json|Tất cả file (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var loaded = ProblemAuthoringDocument.Load(dialog.FileName);
            if (loaded.ProblemId != _problem.Id) throw new InvalidDataException("Bản nháp dùng khuôn input khác. Hãy chọn đúng dạng bài trước.");
            Document = loaded;
            _loading = true;
            txtName.Text = Document.Name; txtCode.Text = Document.Code;
            txtTime.Text = Document.TimeLimitMs.ToString(); txtMemory.Text = Document.MemoryLimitMb.ToString();
            txtDescription.Text = Document.Description; txtInput.Text = Document.Input; txtOutput.Text = Document.Output;
            txtConstraints.Text = Document.Constraints; txtExamples.Text = Document.Examples;
            _loading = false;
            browserPreview.NavigateToString(Document.ToHtml());
        }
        catch (Exception ex)
        {
            _loading = false;
            MessageBox.Show(this, "Không mở được bản nháp: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnExportMarkdown_Click(object sender, RoutedEventArgs e)
    {
        UpdateDocument();
        var dialog = new SaveFileDialog { Title = "Xuất đề dạng Markdown", Filter = "Markdown (*.md)|*.md", FileName = (Document.Code.Length == 0 ? "problem" : Document.Code) + ".md" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, Document.ToMarkdown(), new UTF8Encoding(false)); }
        catch (Exception ex) { MessageBox.Show(this, "Không xuất được Markdown: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void BtnUse_Click(object sender, RoutedEventArgs e)
    {
        var check = CheckDocument();
        if (check.Errors.Count > 0)
        {
            var ask = MessageBox.Show(this, "Bản đề còn lỗi cấu trúc. Dùng tiếp để đóng gói?\n\n" + string.Join("\n", check.Errors), "Bản đề chưa hoàn chỉnh", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (ask != MessageBoxResult.Yes) return;
        }
        else if (check.Warnings.Count > 0)
        {
            var ask = MessageBox.Show(this, "Bản đề có lưu ý cần rà lại. Dùng tiếp?\n\n" + string.Join("\n", check.Warnings), "Còn lưu ý", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
            if (ask != MessageBoxResult.Yes) return;
        }
        Result = Document.Clone();
        DialogResult = true;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
