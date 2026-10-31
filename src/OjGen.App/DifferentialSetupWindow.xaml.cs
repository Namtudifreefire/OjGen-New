using System.IO;
using System.Windows;
using Microsoft.Win32;
using OjGen.Core;

namespace OjGen.Gui;

public partial class DifferentialSetupWindow : Window
{
    private readonly ConfigModel _model;
    public DifferentialSpec? Result { get; private set; }

    public DifferentialSetupWindow(Problem problem, Config config, string referencePath, string seed)
    {
        InitializeComponent();
        Config small = DifferentialTester.CreateSmallConfig(problem, config);
        _model = new ConfigModel(problem, small);
        _ = new ParamForm(_model, paramHost);
        txtReference.Text = referencePath;
        txtCount.Text = "100";
        txtSeed.Text = seed;
    }

    private void BrowseInto(System.Windows.Controls.TextBox target, string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Lời giải (*.cpp;*.cc;*.cxx;*.py;*.exe)|*.cpp;*.cc;*.cxx;*.c++;*.py;*.exe|Tất cả file (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) == true) target.Text = dialog.FileName;
    }

    private void BtnBrowseReference_Click(object sender, RoutedEventArgs e) => BrowseInto(txtReference, "Chọn lời giải chuẩn");
    private void BtnBrowseCandidate_Click(object sender, RoutedEventArgs e) => BrowseInto(txtCandidate, "Chọn lời giải cần kiểm tra");

    private void BtnRun_Click(object sender, RoutedEventArgs e)
    {
        string reference = txtReference.Text.Trim().Trim('"');
        string candidate = txtCandidate.Text.Trim().Trim('"');
        if (reference.Length == 0 || !File.Exists(reference)) { ShowError("Không tìm thấy file lời giải chuẩn."); return; }
        if (candidate.Length == 0 || !File.Exists(candidate)) { ShowError("Không tìm thấy file lời giải cần kiểm tra."); return; }
        if (string.Equals(Path.GetFullPath(reference), Path.GetFullPath(candidate), StringComparison.OrdinalIgnoreCase))
        {
            ShowError("Hãy chọn hai file lời giải khác nhau.");
            return;
        }
        if (!Util.ParseLL(txtCount.Text, out long count) || count < 1 || count > 5000)
        {
            ShowError("Số test đối chiếu phải trong [1, 5000].");
            return;
        }
        if (_model.Validate() is { } configError) { ShowError(configError); return; }
        ulong seed;
        if (txtSeed.Text.Trim().Length == 0) seed = Generator.RandomSeed();
        else if (!Util.ParseULL(txtSeed.Text, out seed)) { ShowError("Seed phải là số nguyên không âm."); return; }

        Result = new DifferentialSpec
        {
            Problem = _model.Problem,
            Cfg = _model.Effective(),
            TestCount = (int)count,
            Seed = seed,
            ReferencePath = reference,
            CandidatePath = candidate,
            IgnoreWhitespace = chkIgnoreWhitespace.IsChecked == true,
        };
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        lblError.Text = "⚠ " + message;
        lblError.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "ErrorBrush");
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
