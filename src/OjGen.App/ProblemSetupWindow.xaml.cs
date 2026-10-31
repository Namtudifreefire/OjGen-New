using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OjGen.Core;

namespace OjGen.Gui;

public partial class ProblemSetupWindow : Window
{
    private ConfigModel _model;
    private ParamForm _form;
    private int _step;
    private bool _loading;

    internal ProblemSetupResult? Result { get; private set; }

    public ProblemSetupWindow(Problem problem, Config config, string name, string code, string solution)
    {
        InitializeComponent();
        _loading = true;
        cmbProblem.ItemsSource = Problems.All;
        cmbProblem.SelectedItem = problem;
        txtName.Text = name.Length == 0 ? problem.Title : name;
        txtCode.Text = code.Length == 0 ? problem.Id : code;
        txtSolution.Text = solution;
        _model = new ConfigModel(problem, config);
        _form = new ParamForm(_model, paramHost);
        _form.Changed += OnConfigChanged;
        _loading = false;
        ShowStep(0);
    }

    private void OnConfigChanged()
    {
        if (!_loading) UpdateStepButtons();
    }

    private void CmbProblem_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || cmbProblem.SelectedItem is not Problem problem) return;
        _model = new ConfigModel(problem);
        _form = new ParamForm(_model, paramHost);
        _form.Changed += OnConfigChanged;
        UpdateStepButtons();
    }

    private void ShowStep(int step)
    {
        _step = Math.Clamp(step, 0, 2);
        pageBasics.Visibility = _step == 0 ? Visibility.Visible : Visibility.Collapsed;
        pageConfig.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        pageReview.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        btnBack.Visibility = _step == 0 ? Visibility.Collapsed : Visibility.Visible;
        btnNext.Visibility = _step == 2 ? Visibility.Collapsed : Visibility.Visible;
        btnFinish.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        lblStep.Text = _step switch
        {
            0 => "Bước 1/3 · Đặt tên bài và chọn khuôn",
            1 => "Bước 2/3 · Thiết lập ràng buộc",
            _ => "Bước 3/3 · Kiểm tra định dạng và lời giải",
        };
        lblStepHint.Text = _step switch
        {
            0 => "Chọn một khuôn input phù hợp với cấu trúc đề. Mã bài sẽ dùng làm tên thư mục output mặc định.",
            1 => "Các tham số được kiểm tra theo giới hạn và quy tắc của khuôn đã chọn.",
            _ => "OjGen sẽ dùng cấu hình này cho kế hoạch test. Có thể lưu hồ sơ để mở lại sau.",
        };
        txtFormat.Text = ((Problem)cmbProblem.SelectedItem).FormatDescription.TrimEnd();
        UpdateStepButtons();
    }

    private string? ValidateBasics()
    {
        if (cmbProblem.SelectedItem is not Problem) return "Hãy chọn khuôn input.";
        if (txtName.Text.Trim().Length == 0) return "Tên bài không được để trống.";
        string code = txtCode.Text.Trim();
        if (!Regex.IsMatch(code, "^[A-Za-z0-9_-]{1,64}$"))
            return "Mã bài chỉ được gồm chữ không dấu, số, dấu gạch ngang hoặc gạch dưới (tối đa 64 ký tự).";
        return null;
    }

    private string? ValidateStep(int step) => step switch
    {
        0 => ValidateBasics(),
        1 => _model.Validate(),
        _ => ValidateBasics() ?? _model.Validate(),
    };

    private void UpdateStepButtons()
    {
        btnNext.IsEnabled = ValidateStep(_step) == null;
        btnFinish.IsEnabled = ValidateStep(2) == null;
    }

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (ValidateStep(_step) is { } error)
        {
            MessageBox.Show(this, error, "Thông tin chưa hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        ShowStep(_step + 1);
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e) => ShowStep(_step - 1);

    private void BtnFinish_Click(object sender, RoutedEventArgs e)
    {
        if (ValidateStep(2) is { } error)
        {
            MessageBox.Show(this, error, "Thông tin chưa hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Result = new ProblemSetupResult
        {
            Problem = _model.Problem,
            Config = _model.Effective(),
            Name = txtName.Text.Trim(),
            Code = txtCode.Text.Trim(),
            SolutionPath = txtSolution.Text.Trim().Trim('"'),
        };
        DialogResult = true;
    }

    private void BtnBrowseSolution_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn lời giải chuẩn",
            Filter = "Lời giải (*.cpp;*.cc;*.cxx;*.py;*.exe)|*.cpp;*.cc;*.cxx;*.c++;*.py;*.exe|Tất cả file (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) == true) txtSolution.Text = dialog.FileName;
    }

    private void TxtSolution_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) txtSolution.Text = files[0];
        e.Handled = true;
    }

    private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ValidateStep(2) is { } error)
        {
            MessageBox.Show(this, error, "Không thể lưu hồ sơ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var dialog = new SaveFileDialog
        {
            Title = "Lưu hồ sơ bài toán",
            Filter = "Hồ sơ OjGen (*.ojproblem.json)|*.ojproblem.json|JSON (*.json)|*.json",
            FileName = txtCode.Text.Trim() + ".ojproblem.json",
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            ProblemProfileFile.From(_model.Problem, _model.Effective(), txtName.Text.Trim(), txtCode.Text.Trim(), txtSolution.Text.Trim().Trim('"')).Save(dialog.FileName);
            MessageBox.Show(this, "Đã lưu hồ sơ bài toán.", "OjGen", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Không lưu được hồ sơ: " + ex.Message, "Lỗi lưu hồ sơ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnLoadProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Mở hồ sơ bài toán",
            Filter = "Hồ sơ OjGen (*.ojproblem.json;*.json)|*.ojproblem.json;*.json|Tất cả file (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var profile = ProblemProfileFile.Load(dialog.FileName);
            var problem = Problems.Find(profile.ProblemId)!;
            _loading = true;
            cmbProblem.SelectedItem = problem;
            txtName.Text = profile.Name;
            txtCode.Text = profile.Code;
            txtSolution.Text = profile.SolutionPath;
            _model = new ConfigModel(problem, profile.ToConfig(problem));
            _form = new ParamForm(_model, paramHost);
            _form.Changed += OnConfigChanged;
            _loading = false;
            ShowStep(0);
        }
        catch (Exception ex)
        {
            _loading = false;
            MessageBox.Show(this, "Không mở được hồ sơ: " + ex.Message, "Lỗi hồ sơ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
