using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using OjGen.Core;

namespace OjGen.Gui;

public partial class ManualTestsWindow : Window
{
    private sealed record ConfigChoice(string Name, Config Config);

    private readonly ObservableCollection<ManualTestSpec> _tests = new();
    private readonly List<ConfigChoice> _choices = new();
    private readonly Problem _problem;
    private ManualTestSpec? _current;
    private bool _switching;

    public IReadOnlyList<ManualTestSpec> Tests { get; private set; } = Array.Empty<ManualTestSpec>();

    public ManualTestsWindow(Problem problem, Config baseConfig, IReadOnlyList<TestGroupSpec> groups,
                             IReadOnlyList<ManualTestSpec> tests)
    {
        InitializeComponent();
        _problem = problem;
        AddChoice("Cấu hình chung", baseConfig);
        foreach (var group in groups) AddChoice("Nhóm: " + group.Name, group.Cfg);
        foreach (var test in tests)
        {
            if (!_choices.Any(choice => ConfigEquals(choice.Config, test.Cfg)))
                AddChoice("Hồ sơ đã lưu: " + test.ConfigName, test.Cfg);
            _tests.Add(test.Clone());
        }
        cmbConfig.ItemsSource = _choices;
        lstTests.ItemsSource = _tests;
        _switching = true;
        if (_tests.Count > 0) lstTests.SelectedIndex = 0;
        _switching = false;
        if (_tests.Count > 0) LoadTest(_tests[0]);
        else BeginNew();
        UpdateCount();
    }

    private void AddChoice(string name, Config config)
    {
        string unique = name;
        int suffix = 2;
        while (_choices.Any(c => c.Name == unique)) unique = name + " " + suffix++;
        _choices.Add(new ConfigChoice(unique, config.Clone()));
    }

    private static bool ConfigEquals(Config a, Config b) =>
        a.Ints.Count == b.Ints.Count && a.Texts.Count == b.Texts.Count &&
        a.Ints.All(kv => b.Ints.TryGetValue(kv.Key, out var value) && value == kv.Value) &&
        a.Texts.All(kv => b.Texts.TryGetValue(kv.Key, out var value) && value == kv.Value);

    private void LoadTest(ManualTestSpec test)
    {
        _current = test;
        _switching = true;
        txtName.Text = test.Name;
        chkSample.IsChecked = test.IsSample;
        txtInput.Text = test.Input;
        txtOutput.Text = test.ExpectedOutput.Length > 0 ? test.ExpectedOutput : test.GeneratedOutput;
        cmbConfig.SelectedItem = _choices.FirstOrDefault(choice => ConfigEquals(choice.Config, test.Cfg));
        _switching = false;
        lblValidation.Text = "";
        UpdateCount();
    }

    private void BeginNew()
    {
        _current = null;
        _switching = true;
        txtName.Text = "test_tay_" + (_tests.Count + 1);
        chkSample.IsChecked = false;
        txtInput.Clear();
        txtOutput.Clear();
        cmbConfig.SelectedIndex = 0;
        lstTests.SelectedItem = null;
        _switching = false;
        lblValidation.Text = "Nhập input hoặc mở file .inp, rồi kiểm tra trước khi lưu.";
        UpdateCount();
    }

    private string? ValidateDraft(out Config? config)
    {
        config = null;
        if (cmbConfig.SelectedItem is not ConfigChoice choice) return "Hãy chọn cấu hình để kiểm tra input.";
        config = choice.Config.Clone();
        if (txtName.Text.Trim().Length == 0) return "Tên test không được để trống.";
        if (_tests.Any(test => !ReferenceEquals(test, _current) && string.Equals(test.Name.Trim(), txtName.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
            return "Tên test đã được dùng; hãy chọn tên khác.";
        if (Generator.ValidateFullConfig(_problem, config) is { } configError) return configError;
        string input = NormalizeInput(txtInput.Text);
        if (_problem.ValidateInput(input, config) is { } inputError) return inputError;
        return null;
    }

    private static string NormalizeInput(string input)
    {
        string normalized = input.Replace("\r\n", "\n").Replace('\r', '\n');
        return normalized.Length > 0 && normalized[^1] != '\n' ? normalized + "\n" : normalized;
    }

    private bool SaveCurrent()
    {
        string? error = ValidateDraft(out var config);
        if (error != null || config == null)
        {
            lblValidation.Text = "✗ " + (error ?? "Không đọc được cấu hình.");
            lblValidation.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "ErrorBrush");
            return false;
        }
        var choice = (ConfigChoice)cmbConfig.SelectedItem;
        if (_current == null)
        {
            _current = new ManualTestSpec();
            _tests.Add(_current);
        }
        _current.Name = txtName.Text.Trim();
        _current.Input = NormalizeInput(txtInput.Text);
        _current.ExpectedOutput = NormalizeInput(txtOutput.Text);
        _current.IsSample = chkSample.IsChecked == true;
        _current.ConfigName = choice.Name;
        _current.Cfg = config;
        _switching = true;
        lstTests.SelectedItem = _current;
        lstTests.Items.Refresh();
        _switching = false;
        lblValidation.Text = "✓ Input hợp lệ; lời giải chuẩn sẽ tạo output khi chạy bộ test.";
        lblValidation.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "SuccessBrush");
        UpdateCount();
        return true;
    }

    private void UpdateCount() => lblCount.Text = $"{_tests.Count} test nhập tay · sample được giữ trong bộ test và tạo output như các test khác.";

    private void LstTests_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_switching || lstTests.SelectedItem is not ManualTestSpec next || ReferenceEquals(next, _current)) return;
        if (_current != null && !SaveCurrent())
        {
            _switching = true;
            lstTests.SelectedItem = _current;
            _switching = false;
            return;
        }
        LoadTest(next);
    }

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        if (_current != null && !SaveCurrent()) return;
        BeginNew();
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        _switching = true;
        _tests.Remove(_current);
        _current = null;
        lstTests.SelectedItem = null;
        _switching = false;
        BeginNew();
    }

    private void BtnOpenInput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Mở file input", Filter = "Input (*.inp;*.txt)|*.inp;*.txt|Tất cả file (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try { txtInput.Text = File.ReadAllText(dialog.FileName); }
        catch (Exception ex) { MessageBox.Show(this, "Không đọc được file: " + ex.Message, "Lỗi đọc input", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void BtnValidate_Click(object sender, RoutedEventArgs e)
    {
        string? error = ValidateDraft(out _);
        lblValidation.Text = error == null ? "✓ Input hợp lệ." : "✗ " + error;
        lblValidation.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, error == null ? "SuccessBrush" : "ErrorBrush");
    }

    private void BtnSaveTest_Click(object sender, RoutedEventArgs e) => SaveCurrent();

    private void BtnFinish_Click(object sender, RoutedEventArgs e)
    {
        if (_current != null && !SaveCurrent()) return;
        if (_current == null && txtInput.Text.Trim().Length > 0 && !SaveCurrent()) return;
        if (_tests.Count == 0)
        {
            Tests = Array.Empty<ManualTestSpec>();
            DialogResult = true;
            return;
        }
        Tests = _tests.Select(test => test.Clone()).ToList();
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
