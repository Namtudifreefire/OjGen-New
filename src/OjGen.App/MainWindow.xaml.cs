using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using OjGen.Core;
using OjGen.Gui.Services;

namespace OjGen.Gui;

public partial class MainWindow : Window
{
    private const int PreviewMaxChars = 8000;
    private const int LogMaxChars = 1_500_000;

    private readonly Settings _st;
    private readonly UiLog _log;
    private ConfigModel? _model;
    private ParamForm? _form;
    private CancellationTokenSource? _cts;
    private bool _running;
    private bool _reviewing;
    private JobResult? _lastResult;
    private JobResult? _lastGeneratedResult;
    private JobSpec? _lastGeneratedSpec;
    private ProblemAuthoringDocument? _authoringDocument;
    private bool _loading = true;
    private bool _updatingSubtypeList;
    private bool _applyingAlgorithmTopic;
    private string _preferredProblemId = "";
    private string _problemName = "";
    private string _problemCode = "";
    private List<TestGroupSpec> _testGroups = new();
    private List<ManualTestSpec> _manualTests = new();
    private List<string> _requiredCoverageTags = new();
    private readonly ObservableCollection<QueuedJobEntry> _jobQueue = new();
    private readonly string _queueStatePath = JobQueuePersistence.DefaultPath;
    private readonly DispatcherTimer _projectAutosaveTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private string _activeProjectPath = "";
    private string _projectAutosavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OjGen", "Recovery", "session.ojproject");
    private ProjectPackageOptions _projectPackage = new();

    private void RefreshQueueMenu()
    {
        mnuQueueRun.Header = $"▶ Chạy hàng đợi ({_jobQueue.Count})";
        mnuQueueRun.IsEnabled = !_running && !_reviewing && _jobQueue.Count > 0;
        mnuQueueManage.IsEnabled = !_running && !_reviewing && _jobQueue.Count > 0;
        mnuQueueAdd.IsEnabled = !_running && !_reviewing;
        PersistQueue();
    }

    public MainWindow()
    {
        InitializeComponent();
        _st = ((App)Application.Current).AppSettings;

        _log = new UiLog(Dispatcher);
        _log.TextAppended += AppendLog;
        _log.ProgressChanged += OnProgress;

        txtHelp.Text = HelpContent.Text;
        string last = AppRegistry.GetString("LastProblem", "");
        var initialProblem = Problems.Find(last) ?? Problems.All[0];
        _preferredProblemId = initialProblem.Id;
        cmbAlgorithmTopic.ItemsSource = AlgorithmTopics.All;
        cmbAlgorithmTopic.SelectedIndex = -1;
        cmbMainCategory.ItemsSource = Problems.All.Select(p => p.MainCategory).Distinct().ToList();
        cmbMainCategory.SelectedItem = initialProblem.MainCategory;
        SetSubtypeOptions(initialProblem.MainCategory, initialProblem.Id);

        mnuAutoTheme.IsChecked = AutoThemeService.IsEnabled;
        UpdateAutoThemeHeader();
        UpdateStatusBar();
        _loading = false;

        string? warn = ((App)Application.Current).SettingsWarning;
        if (!string.IsNullOrEmpty(warn)) _log.Line(warn);
        _projectAutosaveTimer.Tick += (_, _) => SaveProjectSnapshot(_projectAutosavePath, quiet: true);
        _projectAutosaveTimer.Start();
        RestoreAutosaveOnStartup();
        RestoreQueue();
    }

    private OjProjectDocument? CaptureProject()
    {
        if (_model == null) return null;
        Config current = _model.Effective();
        var project = new OjProjectDocument
        {
            ProjectPath = _activeProjectPath,
            ProblemId = _model.Problem.Id,
            ProblemName = _problemName,
            ProblemCode = _problemCode,
            TestCount = Util.ParseLL(txtTests.Text, out long count) && count > 0 && count <= Generator.MaxTests ? (int)count : 10,
            Seed = txtSeed.Text,
            SolutionPath = txtSolution.Text,
            OutputDirectory = txtOutDir.Text,
            Zip = chkZip.IsChecked == true,
            ZipIncomplete = chkZipIncomplete.IsChecked == true,
            Overwrite = chkOverwrite.IsChecked == true,
            Rebuild = chkRebuild.IsChecked == true,
            Resume = chkResume.IsChecked == true,
            Ints = new(current.Ints),
            Texts = new(current.Texts),
            TestPlan = TestPlanProfile.From(_model.Problem, _testGroups, _requiredCoverageTags),
            ManualTests = _manualTests.Select(ProjectManualTest.From).ToList(),
            Statement = _authoringDocument?.Clone(),
            Package = _projectPackage,
            LastTestDirectory = _lastGeneratedResult?.OutDir ?? _lastGeneratedSpec?.OutDir ?? "",
        };
        if (_lastGeneratedSpec is { } last)
        {
            project.LastGeneratedCount = last.TestCount;
            project.LastGeneratedSeed = last.Seed ?? 0;
            project.LastGeneratedProblemId = last.Problem.Id;
            project.LastGeneratedSolutionPath = last.SolutionPath;
            project.LastGeneratedInts = new(last.Cfg.Ints);
            project.LastGeneratedTexts = new(last.Cfg.Texts);
            project.LastGeneratedPlan = TestPlanProfile.From(last.Problem, last.Groups, last.RequiredCoverageTags);
            project.LastGeneratedManualTests = last.ManualTests.Select(ProjectManualTest.From).ToList();
            project.LastGeneratedCoverageTags = last.RequiredCoverageTags.ToList();
        }
        return project;
    }

    private void SaveProjectSnapshot(string path, bool quiet, bool force = false)
    {
        if (!force && (_running || _reviewing)) return;
        try { CaptureProject()?.Save(path); }
        catch (Exception ex)
        {
            if (!quiet) MessageBox.Show(this, "Không lưu được dự án: " + ex.Message, "Lỗi lưu dự án", MessageBoxButton.OK, MessageBoxImage.Error);
            else _log.Line("! Không tự lưu được dự án: " + ex.Message);
        }
    }

    private void RestoreAutosaveOnStartup()
    {
        if (!File.Exists(_projectAutosavePath)) return;
        var answer = MessageBox.Show(this, "OjGen tìm thấy phiên soạn đề đã tự lưu. Khôi phục phiên này?",
            "Khôi phục dự án", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
        if (answer != MessageBoxResult.Yes)
        {
            try { File.Delete(_projectAutosavePath); } catch (Exception) { }
            return;
        }
        try { ApplyProject(OjProjectDocument.Load(_projectAutosavePath)); _log.Line("Đã khôi phục phiên soạn đề tự lưu."); }
        catch (Exception ex) { MessageBox.Show(this, "Không khôi phục được phiên tự lưu: " + ex.Message, "Lỗi khôi phục", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void ApplyProject(OjProjectDocument project)
    {
        Problem problem = Problems.Find(project.ProblemId) ?? throw new InvalidDataException("Không tìm thấy khuôn bài trong dự án.");
        Config config = problem.DefaultConfig();
        foreach (var parameter in problem.Params)
        {
            if (parameter.Type == ParamType.Text)
            {
                if (project.Texts.TryGetValue(parameter.Key, out string? value)) config.SetText(parameter.Key, value);
            }
            else if (project.Ints.TryGetValue(parameter.Key, out long value)) config.Set(parameter.Key, value);
        }
        if (Generator.ValidateFullConfig(problem, config) is { } invalidConfig)
            throw new InvalidDataException("Cấu hình trong dự án không hợp lệ: " + invalidConfig);
        if (project.Statement != null && !string.Equals(project.Statement.ProblemId, problem.Id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Bản đề trong dự án đang gắn với một khuôn khác.");
        List<TestGroupSpec> restoredGroups = project.TestPlan.Groups.Count == 0 ? new() : project.TestPlan.ToGroups(problem);
        List<ManualTestSpec> restoredManualTests = project.ManualTests.Select(test => test.ToSpec(problem)).ToList();
        foreach (var manual in restoredManualTests)
        {
            if (Generator.ValidateFullConfig(problem, manual.Cfg) is { } manualConfigError)
                throw new InvalidDataException($"Cấu hình test '{manual.Name}' không hợp lệ: {manualConfigError}");
            if (problem.ValidateInput(manual.Input, manual.Cfg) is { } manualInputError)
                throw new InvalidDataException($"Test '{manual.Name}' không hợp lệ: {manualInputError}");
        }
        JobSpec? restoredLastSpec = null;
        JobResult? restoredLastResult = null;
        if (project.LastGeneratedCount > 0 && project.LastGeneratedProblemId == project.ProblemId && Directory.Exists(project.LastTestDirectory))
        {
            var generatedConfig = problem.DefaultConfig();
            foreach (var item in project.LastGeneratedInts) generatedConfig.Set(item.Key, item.Value);
            foreach (var item in project.LastGeneratedTexts) generatedConfig.SetText(item.Key, item.Value);
            if (Generator.ValidateFullConfig(problem, generatedConfig) is { } generatedConfigError)
                throw new InvalidDataException("Cấu hình của bộ test đã tạo không hợp lệ: " + generatedConfigError);
            List<TestGroupSpec> generatedGroups = project.LastGeneratedPlan.Groups.Count == 0 ? new() : project.LastGeneratedPlan.ToGroups(problem);
            foreach (var group in generatedGroups)
                if (Generator.ValidateFullConfig(problem, group.Cfg) is { } groupError)
                    throw new InvalidDataException($"Nhóm test đã tạo '{group.Name}' không hợp lệ: {groupError}");
            List<ManualTestSpec> generatedManual = project.LastGeneratedManualTests.Select(test => test.ToSpec(problem)).ToList();
            foreach (var manual in generatedManual)
            {
                if (Generator.ValidateFullConfig(problem, manual.Cfg) is { } manualConfigError)
                    throw new InvalidDataException($"Cấu hình test đã tạo '{manual.Name}' không hợp lệ: {manualConfigError}");
                if (problem.ValidateInput(manual.Input, manual.Cfg) is { } manualInputError)
                    throw new InvalidDataException($"Test đã tạo '{manual.Name}' không hợp lệ: {manualInputError}");
            }
            int generatedCount = project.LastGeneratedCount - generatedManual.Count;
            if (generatedCount < 0 || (generatedGroups.Count > 0 && generatedGroups.Sum(group => group.TestCount) != generatedCount))
                throw new InvalidDataException("Số test của kế hoạch không khớp bộ test đã tạo.");
            restoredLastSpec = new JobSpec
            {
                Problem = problem, Cfg = generatedConfig, TestCount = project.LastGeneratedCount,
                GeneratedTestCount = generatedCount, Seed = project.LastGeneratedSeed,
                SolutionPath = project.LastGeneratedSolutionPath, OutDir = project.LastTestDirectory,
                ProblemName = project.ProblemName, ProblemCode = project.ProblemCode,
                Groups = generatedGroups, ManualTests = generatedManual,
                RequiredCoverageTags = project.LastGeneratedCoverageTags.ToList(),
            };
            int inputs = FileManager.ListFilesWithExt(project.LastTestDirectory, ".inp").Count;
            restoredLastResult = new JobResult
            {
                OutDir = project.LastTestDirectory, InputCount = inputs, Seed = project.LastGeneratedSeed,
                Status = FileManager.ListFilesWithExt(project.LastTestDirectory, ".out").Count >= inputs ? SetStatus.Complete : SetStatus.InputOnly,
            };
        }

        _loading = true;
        cmbAlgorithmTopic.SelectedItem = null;
        cmbMainCategory.SelectedItem = problem.MainCategory;
        SetSubtypeOptions(problem.MainCategory, problem.Id);
        SetProblemModel(problem, config);
        _problemName = project.ProblemName;
        _problemCode = project.ProblemCode;
        txtTests.Text = project.TestCount.ToString();
        txtTests.IsEnabled = project.TestPlan.Groups.Count == 0;
        txtSeed.Text = project.Seed;
        txtSolution.Text = project.SolutionPath;
        txtOutDir.Text = project.OutputDirectory;
        chkZip.IsChecked = project.Zip;
        chkZipIncomplete.IsChecked = project.ZipIncomplete;
        chkOverwrite.IsChecked = project.Overwrite;
        chkRebuild.IsChecked = project.Rebuild;
        chkResume.IsChecked = project.Resume;
        _testGroups = restoredGroups;
        _requiredCoverageTags = project.TestPlan.RequiredCoverageTags?.ToList() ?? new();
        _manualTests = restoredManualTests;
        _authoringDocument = project.Statement?.Clone();
        _projectPackage = project.Package ?? new ProjectPackageOptions();
        _activeProjectPath = project.ProjectPath;
        lblSetupSummary.Text = $"Bài: {_problemName} · mã {_problemCode} · khuôn {problem.Title}";
        lblGroupPlan.Text = _testGroups.Count == 0
            ? "Một cấu hình chung cho toàn bộ test."
            : $"{_testGroups.Count} nhóm · {_testGroups.Sum(group => group.TestCount)} test: " + string.Join(", ", _testGroups.Select(group => $"{group.Name} ({group.TestCount})"));
        lblManualTests.Text = _manualTests.Count == 0 ? "Chưa có test nhập tay." : $"{_manualTests.Count} test nhập tay · {_manualTests.Count(test => test.IsSample)} sample";

        _lastGeneratedSpec = restoredLastSpec;
        _lastGeneratedResult = restoredLastResult;
        _loading = false;
        OnFormChanged();
    }

    private void MnuSaveProject_Click(object sender, RoutedEventArgs e)
    {
        if (_activeProjectPath.Length == 0) { MnuSaveProjectAs_Click(sender, e); return; }
        SaveProjectSnapshot(_activeProjectPath, quiet: false);
    }

    private void MnuSaveProjectAs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Title = "Lưu dự án soạn đề", Filter = "OjGen project (*.ojproject)|*.ojproject", DefaultExt = ".ojproject", AddExtension = true,
            FileName = _problemCode.Length > 0 ? _problemCode + ".ojproject" : "problem.ojproject" };
        if (dialog.ShowDialog(this) != true) return;
        _activeProjectPath = dialog.FileName;
        SaveProjectSnapshot(dialog.FileName, quiet: false);
    }

    private void MnuSavePreset_Click(object sender, RoutedEventArgs e)
    {
        if (_model == null) return;
        bool hasFixedSeed = Util.ParseULL(txtSeed.Text, out ulong fixedSeed);
        if (!string.IsNullOrWhiteSpace(txtSeed.Text) && !hasFixedSeed)
        {
            MessageBox.Show(this, "Seed cần là số nguyên không âm hoặc để trống để dùng seed ngẫu nhiên.", "Seed không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Directory.CreateDirectory(AuthoringPresetStore.DefaultDirectory);
        var dialog = new SaveFileDialog
        {
            Title = "Lưu preset cấu hình ra đề",
            Filter = "OjGen preset (*.ojpreset.json)|*.ojpreset.json",
            DefaultExt = ".ojpreset.json", AddExtension = true,
            FileName = (_problemCode.Length > 0 ? _problemCode : _model.Problem.Id) + ".ojpreset.json",
            InitialDirectory = AuthoringPresetStore.DefaultDirectory,
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var preset = new AuthoringPreset
            {
                Name = Path.GetFileName(dialog.FileName).Replace(".ojpreset.json", "", StringComparison.OrdinalIgnoreCase),
                ProblemId = _model.Problem.Id, ProblemName = _problemName, ProblemCode = _problemCode,
                ReferenceSolution = txtSolution.Text.Trim(),
                TestCount = _testGroups.Count > 0 ? _testGroups.Sum(group => group.TestCount) :
                    (Util.ParseLL(txtTests.Text, out long parsedTests) && parsedTests is > 0 and <= Generator.MaxTests ? (int)parsedTests : 10),
                SeedPolicy = hasFixedSeed ? "Fixed" : "Random", Seed = hasFixedSeed ? fixedSeed : null,
                TestPlan = TestPlanProfile.From(_model.Problem, _testGroups, _requiredCoverageTags),
                RequiredCoverageTags = _requiredCoverageTags.ToList(), Ints = new(_model.Effective().Ints), Texts = new(_model.Effective().Texts),
                ValidatorPath = _projectPackage.ValidatorPath, CheckerPath = _projectPackage.CheckerPath,
                CheckerArguments = _projectPackage.CheckerArguments, CheckerTimeoutMs = _projectPackage.CheckerTimeoutMs,
                ContractSuitePath = _projectPackage.ContractSuitePath, CandidateSolution = _projectPackage.CandidateSolution,
            };
            preset.Save(dialog.FileName);
            MessageBox.Show(this, "Đã lưu preset:\n" + dialog.FileName, "Preset ra đề", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, "Không lưu được preset: " + ex.Message, "Lỗi preset", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void MnuOpenPreset_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AuthoringPresetStore.DefaultDirectory);
        var dialog = new OpenFileDialog
        {
            Title = "Chọn preset ra đề", Filter = "OjGen preset (*.ojpreset.json)|*.ojpreset.json|JSON (*.json)|*.json",
            CheckFileExists = true, InitialDirectory = AuthoringPresetStore.DefaultDirectory,
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var preset = AuthoringPreset.Load(dialog.FileName);
            Problem problem = Problems.Find(preset.ProblemId) ?? throw new InvalidDataException("Không tìm thấy dạng bài trong OjGen hiện tại: " + preset.ProblemId);
            Config config = problem.DefaultConfig();
            foreach (var parameter in problem.Params)
            {
                if (parameter.Type == ParamType.Text && preset.Texts.TryGetValue(parameter.Key, out string? text)) config.SetText(parameter.Key, text);
                else if (parameter.Type != ParamType.Text && preset.Ints.TryGetValue(parameter.Key, out long value)) config.Set(parameter.Key, value);
            }
            if (Generator.ValidateFullConfig(problem, config) is { } configError) throw new InvalidDataException("Cấu hình preset không hợp lệ: " + configError);
            var groups = preset.TestPlan.Groups.Count > 0 ? preset.TestPlan.ToGroups(problem) : new List<TestGroupSpec>();
            _loading = true;
            cmbAlgorithmTopic.SelectedItem = null;
            cmbMainCategory.SelectedItem = problem.MainCategory;
            SetSubtypeOptions(problem.MainCategory, problem.Id);
            SetProblemModel(problem, config);
            _loading = false;
            _problemName = preset.ProblemName;
            _problemCode = preset.ProblemCode;
            _testGroups = groups;
            _requiredCoverageTags = preset.RequiredCoverageTags.ToList();
            _manualTests.Clear();
            _authoringDocument = null;
            txtTests.Text = (groups.Count > 0 ? groups.Sum(group => group.TestCount) : preset.TestCount).ToString();
            txtTests.IsEnabled = groups.Count == 0;
            paramHost.IsEnabled = groups.Count == 0;
            txtSeed.Text = preset.SeedPolicy == "Fixed" && preset.Seed.HasValue ? Util.LL(preset.Seed.Value) : "";
            _projectPackage = new ProjectPackageOptions
            {
                ValidatorPath = preset.ValidatorPath, CheckerPath = preset.CheckerPath, CheckerArguments = preset.CheckerArguments,
                CheckerTimeoutMs = preset.CheckerTimeoutMs, ContractSuitePath = preset.ContractSuitePath, CandidateSolution = preset.CandidateSolution,
            };
            if (preset.ReferenceSolution.Length > 0) txtSolution.Text = preset.ReferenceSolution;
            lblSetupSummary.Text = $"Bài: {_problemName} · mã {_problemCode} · khuôn {problem.Title}";
            lblGroupPlan.Text = groups.Count == 0 ? "Một cấu hình chung cho toàn bộ test." :
                $"{groups.Count} nhóm · {groups.Sum(group => group.TestCount)} test: " + string.Join(", ", groups.Select(group => $"{group.Name} ({group.TestCount})"));
            lblManualTests.Text = "Chưa có test nhập tay.";
            OnFormChanged();
            _log.Line("Đã áp dụng preset ra đề: " + preset.Name);
        }
        catch (Exception ex)
        {
            _loading = false;
            MessageBox.Show(this, "Không áp dụng được preset: " + ex.Message, "Lỗi preset", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MnuOpenProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Mở dự án soạn đề", Filter = "OjGen project (*.ojproject)|*.ojproject|All files (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var project = OjProjectDocument.Load(dialog.FileName);
            project.ProjectPath = dialog.FileName;
            ApplyProject(project);
            _activeProjectPath = dialog.FileName;
            _log.Line("Đã mở dự án: " + dialog.FileName);
        }
        catch (Exception ex) { MessageBox.Show(this, "Không mở được dự án: " + ex.Message, "Lỗi mở dự án", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            MnuSaveProject_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.S)
        {
            MnuSaveProjectAs_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
        {
            MnuOpenProject_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    // ============================================================ DẠNG BÀI / FORM

    private void CmbMainCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _applyingAlgorithmTopic || cmbMainCategory.SelectedItem is not string category) return;
        cmbAlgorithmTopic.SelectedItem = null;
        SetSubtypeOptions(category, "");
    }

    private void CmbAlgorithmTopic_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _applyingAlgorithmTopic) return;
        if (cmbAlgorithmTopic.SelectedItem is not AlgorithmTopic topic)
        {
            lblAlgorithmHint.Text = "Tùy chọn: chọn một chủ đề để OjGen gợi ý khuôn input phù hợp.";
            return;
        }

        var recommended = Problems.Find(topic.RecommendedProblemId);
        if (recommended == null)
        {
            lblAlgorithmHint.Text = "Chưa có khuôn input phù hợp trong OjGen.";
            return;
        }

        _applyingAlgorithmTopic = true;
        try
        {
            cmbMainCategory.SelectedItem = recommended.MainCategory;
            SetSubtypeOptions(recommended.MainCategory, recommended.Id);
            lblAlgorithmHint.Inlines.Clear();
            lblAlgorithmHint.Inlines.Add(new Run($"Khuôn input gợi ý: {recommended.Title}. "));
            if (topic.ExampleProblemUrl is { } exampleUrl && topic.ExampleProblem is { } exampleProblem)
            {
                lblAlgorithmHint.Inlines.Add(new Run("Bài mẫu: "));
                var link = new Hyperlink(new Run(exampleProblem)) { NavigateUri = new Uri(exampleUrl) };
                link.RequestNavigate += (_, e) =>
                {
                    OpenPath(e.Uri.AbsoluteUri);
                    e.Handled = true;
                };
                lblAlgorithmHint.Inlines.Add(link);
                lblAlgorithmHint.Inlines.Add(new Run(". "));
            }
            lblAlgorithmHint.Inlines.Add(new Run("Hãy chỉnh giới hạn theo đề và gắn lời giải chuẩn để tạo output."));
        }
        finally
        {
            _applyingAlgorithmTopic = false;
        }
    }

    private void SetSubtypeOptions(string category, string preferredId)
    {
        var options = Problems.All.Where(p => p.MainCategory == category).ToList();
        if (options.Count == 0) return;

        Problem selected = options.FirstOrDefault(p => p.Id == preferredId) ?? options[0];
        _updatingSubtypeList = true;
        cmbProblem.ItemsSource = options;
        cmbProblem.SelectedItem = selected;
        _updatingSubtypeList = false;
        ApplyProblem(selected);
    }

    private void CmbProblem_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingSubtypeList || cmbProblem.SelectedItem is not Problem p) return;
        if (!_applyingAlgorithmTopic && cmbAlgorithmTopic.SelectedItem is AlgorithmTopic topic &&
            topic.RecommendedProblemId != p.Id)
            cmbAlgorithmTopic.SelectedItem = null;
        ApplyProblem(p);
    }

    private void ApplyProblem(Problem p)
    {
        _problemName = "";
        _problemCode = "";
        _testGroups.Clear();
        _requiredCoverageTags.Clear();
        _manualTests.Clear();
        txtTests.Text = "10";
        txtTests.IsEnabled = true;
        paramHost.IsEnabled = true;
        lblSetupSummary.Text = "Chưa lưu tên bài riêng; sẽ dùng tên khuôn sinh input.";
        lblGroupPlan.Text = "Một cấu hình chung cho toàn bộ test.";
        lblManualTests.Text = "Chưa có test nhập tay.";
        _authoringDocument = null;
        _lastGeneratedResult = null;
        _lastGeneratedSpec = null;
        SetProblemModel(p, p.DefaultConfig());
    }

    private void SetProblemModel(Problem p, Config config)
    {
        _preferredProblemId = p.Id;
        _model = new ConfigModel(p, config);
        _form = new ParamForm(_model, paramHost);
        _form.Changed += OnFormChanged;

        txtFormat.Text = p.FormatDescription.TrimEnd();
        string sol = _st.SolutionFor(p.Id);
        txtSolution.Text = sol.Length > 0 ? sol : _st.Solution;
        lblOutHint.Text = "Để trống thư mục đầu ra = " + JobPlanner.DefaultOutDir(p, _st, _problemCode);
        txtPreview.Clear();
        resultPanel.Visibility = Visibility.Collapsed;
        if (!_loading) AppRegistry.SetString("LastProblem", p.Id);
        OnFormChanged();
    }

    private void OnFormChanged()
    {
        string? err = _model?.Validate();
        lblValidation.Text = err == null ? "" : "⚠ " + err;
        lblValidation.Visibility = err == null ? Visibility.Collapsed : Visibility.Visible;
        UpdateButtons(err == null);
    }

    private void UpdateButtons(bool formValid)
    {
        btnRun.IsEnabled = !_running && !_reviewing && formValid;
        btnPreview.IsEnabled = !_running && !_reviewing && formValid;
        btnCancel.IsEnabled = _running;
        cmbAlgorithmTopic.IsEnabled = !_running && !_reviewing;
        cmbMainCategory.IsEnabled = !_running && !_reviewing;
        cmbProblem.IsEnabled = !_running && !_reviewing;
        mnuSettings.IsEnabled = !_running && !_reviewing;
        mnuStatement.IsEnabled = !_running && !_reviewing;
        mnuPackage.IsEnabled = !_running && !_reviewing;
        mnuScaffold.IsEnabled = !_running && !_reviewing;
        mnuRegen.IsEnabled = !_running && !_reviewing;
        mnuZipDir.IsEnabled = !_running && !_reviewing;
        mnuSelfTest.IsEnabled = !_running && !_reviewing;
        RefreshQueueMenu();
    }

    private JobPlanner.Options ReadOptions() => new()
    {
        TestsText = txtTests.Text,
        SeedText = txtSeed.Text,
        SolutionPath = txtSolution.Text,
        OutDirText = txtOutDir.Text,
        Zip = chkZip.IsChecked == true,
        ZipIncomplete = chkZipIncomplete.IsChecked == true,
        Overwrite = chkOverwrite.IsChecked == true,
        Rebuild = chkRebuild.IsChecked == true,
        Resume = chkResume.IsChecked == true,
        ProblemName = _problemName,
        ProblemCode = _problemCode,
        Groups = _testGroups,
        RequiredCoverageTags = _requiredCoverageTags,
        ManualTests = _manualTests,
    };

    private void BtnSetupProblem_Click(object sender, RoutedEventArgs e)
    {
        if (_running || _reviewing || _model == null) return;
        string oldProblemId = _model.Problem.Id;
        var dialog = new ProblemSetupWindow(_model.Problem, _model.Effective(), _problemName, _problemCode, txtSolution.Text) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is not { } setup) return;

        _loading = true;
        cmbMainCategory.SelectedItem = setup.Problem.MainCategory;
        cmbProblem.ItemsSource = Problems.All.Where(p => p.MainCategory == setup.Problem.MainCategory).ToList();
        cmbProblem.SelectedItem = setup.Problem;
        if (cmbAlgorithmTopic.SelectedItem is AlgorithmTopic topic && topic.RecommendedProblemId != setup.Problem.Id)
            cmbAlgorithmTopic.SelectedItem = null;
        _loading = false;

        _testGroups.Clear();
        _lastGeneratedResult = null;
        _lastGeneratedSpec = null;
        if (setup.Problem.Id != oldProblemId) _manualTests.Clear();
        if (setup.Problem.Id != oldProblemId)
        {
            _authoringDocument = null;
        }
        txtTests.IsEnabled = true;
        paramHost.IsEnabled = true;
        lblGroupPlan.Text = "Một cấu hình chung cho toàn bộ test.";
        lblManualTests.Text = _manualTests.Count == 0
            ? "Chưa có test nhập tay."
            : $"{_manualTests.Count} test nhập tay · {_manualTests.Count(test => test.IsSample)} sample";
        _problemName = setup.Name;
        _problemCode = setup.Code;
        if (_authoringDocument != null) { _authoringDocument.Name = setup.Name; _authoringDocument.Code = setup.Code; }
        SetProblemModel(setup.Problem, setup.Config);
        txtSolution.Text = setup.SolutionPath;
        lblSetupSummary.Text = $"Bài: {_problemName} · mã {_problemCode} · khuôn {setup.Problem.Title}";
        lblOutHint.Text = "Để trống thư mục đầu ra = " + JobPlanner.DefaultOutDir(setup.Problem, _st, _problemCode);
    }

    private void BtnTestPlan_Click(object sender, RoutedEventArgs e)
    {
        if (_running || _reviewing || _model == null) return;
        int count = Util.ParseLL(txtTests.Text, out long parsed) && parsed >= 1 && parsed <= Generator.MaxTests ? (int)parsed : 10;
        var dialog = new TestPlanWindow(_model.Problem, _model.Effective(), count, _testGroups, _requiredCoverageTags) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        _testGroups = dialog.Groups.Select(group => group.Clone()).ToList();
        _requiredCoverageTags = dialog.RequiredCoverageTags.ToList();
        int total = _testGroups.Sum(group => group.TestCount);
        txtTests.Text = total.ToString();
        txtTests.IsEnabled = false;
        paramHost.IsEnabled = false;
        lblGroupPlan.Text = $"{_testGroups.Count} nhóm · {total} test: " + string.Join(", ", _testGroups.Select(g => $"{g.Name} ({g.TestCount})"));
    }

    private void BtnManualTests_Click(object sender, RoutedEventArgs e)
    {
        if (_running || _reviewing || _model == null) return;
        var dialog = new ManualTestsWindow(_model.Problem, _model.Effective(), _testGroups, _manualTests) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        _manualTests = dialog.Tests.Select(test => test.Clone()).ToList();
        lblManualTests.Text = _manualTests.Count == 0
            ? "Chưa có test nhập tay."
            : $"{_manualTests.Count} test nhập tay · {_manualTests.Count(test => test.IsSample)} sample";
    }

    private async void BtnDifferential_Click(object sender, RoutedEventArgs e)
    {
        if (_running || _reviewing || _model == null) return;
        var setup = new DifferentialSetupWindow(_model.Problem, _model.Effective(), txtSolution.Text, txtSeed.Text) { Owner = this };
        if (setup.ShowDialog() != true || setup.Result is not { } spec) return;
        DifferentialResult? differential = null;
        var run = await RunWork("So sánh lời giải", token =>
        {
            differential = DifferentialTester.Run(spec, _st, _log, token);
            return new JobResult
            {
                ExitCode = differential.Ok ? 0 : differential.Cancelled ? 1 : 2,
                Cancelled = differential.Cancelled,
                InputCount = differential.Tested,
                Error = differential.Error,
            };
        });
        if (run == null || differential == null)
        {
            MessageBox.Show(this, "Không thể hoàn tất kiểm thử đối chiếu. Xem tab Nhật ký.", "Lỗi đối chiếu", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        var result = new DifferentialResultWindow(differential, spec.Seed) { Owner = this };
        result.ShowDialog();
    }

    private bool OpenStatementEditor()
    {
        if (_model == null) return false;
        var samples = _manualTests.Select(test => test.Clone()).ToList();
        var editor = new StatementEditorWindow(_model.Problem, _model.Effective(), _problemName, _problemCode,
            samples, _authoringDocument) { Owner = this };
        if (editor.ShowDialog() != true || editor.Result is not { } document) return false;
        _authoringDocument = document;
        _problemName = document.Name;
        _problemCode = document.Code;
        lblSetupSummary.Text = $"Bài: {_problemName} · mã {_problemCode} · khuôn {_model.Problem.Title}";
        lblOutHint.Text = "Để trống thư mục đầu ra = " + JobPlanner.DefaultOutDir(_model.Problem, _st, _problemCode);
        return true;
    }

    private void MnuStatement_Click(object sender, RoutedEventArgs e)
    {
        if (_running || _reviewing) return;
        OpenStatementEditor();
    }

    private async void MnuPackage_Click(object sender, RoutedEventArgs e)
    {
        if (_running || _reviewing || _model == null) return;
        if (_authoringDocument == null && !OpenStatementEditor()) return;
        var samples = _manualTests.Where(test => test.IsSample).ToList();
        string testDirectory = _lastGeneratedResult?.OutDir ?? "";
        int testCount = _lastGeneratedResult?.InputCount ?? 0;
        int expectedTestCount = _lastGeneratedSpec?.TestCount ?? testCount;
        int sampleCount = samples.Count;
        string parent = Path.GetFullPath(_st.OutputRoot);
        var dialog = new PackageWindow(_authoringDocument!.Name, _authoringDocument.Code, parent, testDirectory,
            txtSolution.Text, sampleCount, testCount, _projectPackage) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is not { } options) return;
        _projectPackage.Capture(options);

        var auditDocument = _authoringDocument.Clone();
        var auditProblem = _model.Problem;
        var auditConfig = _model.Effective();
        var auditManualTests = _manualTests.Select(test => test.Clone()).ToList();
        var auditSettings = _st.Clone();
        var generatedSpec = _lastGeneratedSpec == null ? null : CloneJobSpec(_lastGeneratedSpec);
        ulong? recordedTestSeed = _lastGeneratedResult?.Seed;
        ulong auditSeed = recordedTestSeed ?? Generator.RandomSeed();
        ReleaseAuditReport? audit = null;
        var auditOptions = new ReleaseAuditOptions
        {
            IncludeTests = options.IncludeTests,
            TestDirectory = options.TestDirectory,
            ExpectedTestCount = options.IncludeTests ? expectedTestCount : 0,
            ReferenceSolution = options.ReferenceSolution,
            ValidatorPath = options.ValidatorPath,
            CheckerPath = options.CheckerPath,
            CheckerArguments = options.CheckerArguments,
            CheckerTimeoutMs = options.CheckerTimeoutMs,
            ContractSuitePath = options.ContractSuitePath,
            CandidateSolution = options.CandidateSolution,
            Seed = auditSeed,
            GeneratedSpec = generatedSpec,
        };
        options.GeneratedSpec = generatedSpec;
        var auditRun = await RunWork("Kiểm định phát hành", token =>
        {
            audit = ReleaseAuditService.Run(auditOptions, auditDocument, auditProblem,
                auditConfig, auditManualTests, auditSettings, _log, token);
            return new JobResult
            {
                ExitCode = audit.HasErrors ? 1 : audit.Cancelled ? 1 : 0,
                Cancelled = audit.Cancelled,
                InputCount = auditOptions.ExpectedTestCount,
            };
        });
        if (auditRun == null || audit == null)
        {
            MessageBox.Show(this, "Không thể hoàn tất kiểm định. Xem tab Nhật ký.", "Kiểm định lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        if (new ReleaseAuditWindow(audit) { Owner = this }.ShowDialog() != true) return;
        options.AuditReport = audit.ToText();
        options.TestSeed = recordedTestSeed;

        ProblemPackageResult? package = null;
        var run = await RunWork("Đóng gói bài", _ =>
        {
            package = ProblemPackageExporter.Export(options, auditDocument, auditProblem,
                auditConfig, auditManualTests);
            _log.Line("Đã tạo gói đề: " + package.Directory);
            if (package.ZipPath.Length > 0) _log.Line("ZIP: " + package.ZipPath);
            return new JobResult { ExitCode = 0, OutDir = package.Directory, ZipPath = package.ZipPath, InputCount = package.InputCount };
        });
        if (run == null || package == null)
        {
            MessageBox.Show(this, "Không đóng gói được. Xem tab Nhật ký; thư mục gói có thể còn một phần nếu lỗi ghi file.", "Lỗi đóng gói", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        string auditStatus = audit.WarningCount > 0 ? $"Đạt có {audit.WarningCount} lưu ý" : "Đạt";
        string summary = $"Đã tạo gói · kiểm định: {auditStatus}\n{package.Directory}\n\n{package.InputCount} input · {package.OutputCount} output · {package.SampleCount} sample" +
                         (package.ZipPath.Length > 0 ? "\nZIP: " + package.ZipPath : "");
        if (MessageBox.Show(this, summary + "\n\nMở thư mục gói?", "Đóng gói xong", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            OpenPath(package.Directory);
    }

    private void MnuScaffold_Click(object sender, RoutedEventArgs e)
    {
        if (_running || _reviewing || _model == null) return;
        new ScaffoldWindow(_model.Problem, _model.Effective()) { Owner = this }.ShowDialog();
    }

    // ============================================================ CHỌN FILE / THƯ MỤC

    private void BtnRandomSeed_Click(object sender, RoutedEventArgs e) =>
        txtSeed.Text = Util.LL(Generator.RandomSeed());

    private void BtnBrowseSolution_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Chọn lời giải chuẩn",
            Filter = "Lời giải (*.c;*.cpp;*.cc;*.cxx;*.py;*.java;*.kt;*.pas;*.pp;*.cs;*.rs;*.go;*.exe;*.ojrun.json)|*.c;*.cpp;*.cc;*.cxx;*.c++;*.py;*.java;*.kt;*.pas;*.pp;*.cs;*.rs;*.go;*.exe;*.ojrun.json|Tất cả file (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog(this) == true) txtSolution.Text = dlg.FileName;
    }

    private void BtnBrowseOut_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục chứa bộ test" };
        if (dlg.ShowDialog(this) == true) txtOutDir.Text = dlg.FolderName;
    }

    private void TxtSolution_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void TxtSolution_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            txtSolution.Text = files[0];
        e.Handled = true;
    }

    // ============================================================ XEM TRƯỚC

    private async void BtnPreview_Click(object sender, RoutedEventArgs e)
    {
        if (_model == null || _running || _reviewing) return;
        if (_model.Validate() is { } verr)
        {
            MessageBox.Show(this, verr, "Cấu hình chưa hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        ulong seed;
        bool randomSeed = txtSeed.Text.Trim().Length == 0;
        if (randomSeed) seed = Generator.RandomSeed();
        else if (!Util.ParseULL(txtSeed.Text, out seed))
        {
            MessageBox.Show(this, "Seed phải là số nguyên không âm.", "Seed không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var prob = _model.Problem;
        var groups = _testGroups.Count > 0
            ? _testGroups.Select(g => g.Clone()).ToList()
            : new List<TestGroupSpec> { new() { Name = "Test mẫu", TestCount = 1, Cfg = _model.Effective() } };
        btnPreview.IsEnabled = false;
        lblPreviewInfo.Text = "Đang sinh và kiểm tra input mẫu cho từng nhóm…";
        try
        {
            var preview = await Task.Run(() => CreateGroupSamples(prob, groups, seed, _manualTests));
            if (!preview.Valid) throw new InvalidDataException(preview.Error);
            txtPreview.Text = preview.Text;
            lblPreviewInfo.Text = $"Đã kiểm tra {groups.Count} input sinh mẫu + {_manualTests.Count} test tay ({Util.FmtBytes((ulong)preview.TotalChars)}). " +
                (randomSeed ? $"Seed ngẫu nhiên: {Util.LL(seed)}." : $"Seed {Util.LL(seed)}.");
        }
        catch (Exception ex)
        {
            lblPreviewInfo.Text = "Không xem trước được: " + ex.Message;
        }
        finally
        {
            OnFormChanged();
        }
    }

    // ============================================================ CHẠY CÔNG VIỆC NỀN

    private void SetRunning(bool running, string status)
    {
        _running = running;
        lblStatus.Text = status;
        barProgress.IsIndeterminate = running;
        if (!running) { barProgress.Value = 0; lblProgress.Text = ""; }
        OnFormChanged();
    }

    /// <summary>Chạy một công việc nặng ở luồng nền, ghi nhật ký vào tab Nhật ký.</summary>
    private async Task<JobResult?> RunWork(string title, Func<CancellationToken, JobResult> work)
    {
        if (_running) return null;
        resultPanel.Visibility = Visibility.Collapsed;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        SetRunning(true, title + "…");
        _log.Line("");
        _log.Line($"──────── {DateTime.Now:HH:mm:ss} · {title} ────────");
        JobResult? result = null;
        try
        {
            result = await Task.Run(() => work(token));
        }
        catch (Exception ex)
        {
            _log.Line("LỖI không mong đợi: " + ex.Message);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetRunning(false, "Sẵn sàng");
        }
        _lastResult = result;
        return result;
    }

    private sealed record GroupSamplePreview(bool Valid, string Error, string Text, int TotalChars);

    private static GroupSamplePreview CreateGroupSamples(Problem problem, IReadOnlyList<TestGroupSpec> groups, ulong baseSeed,
                                                          IReadOnlyList<ManualTestSpec>? manualTests = null)
    {
        const int displayLimit = 40_000;
        var output = new StringBuilder();
        long totalChars = 0;
        if (manualTests != null)
        {
            foreach (var manual in manualTests)
            {
                if (Generator.ValidateFullConfig(problem, manual.Cfg) is { } configError)
                    return new(false, $"Test tay '{manual.Name}': {configError}", output.ToString(), (int)Math.Min(int.MaxValue, totalChars));
                if (problem.ValidateInput(manual.Input, manual.Cfg) is { } inputError)
                    return new(false, $"Test tay '{manual.Name}' không hợp lệ: {inputError}", output.ToString(), (int)Math.Min(int.MaxValue, totalChars));
                totalChars += manual.Input.Length;
                if (output.Length < displayLimit)
                {
                    output.Append("=== ").Append(manual.Name).Append(manual.IsSample ? " · sample" : " · test nhập tay").Append(" ===\n");
                    int remaining = Math.Max(0, displayLimit - output.Length);
                    if (manual.Input.Length <= remaining) output.Append(manual.Input);
                    else output.Append(manual.Input.AsSpan(0, remaining)).Append("\n… (đã cắt phần xem trước)");
                    output.Append('\n');
                }
            }
        }
        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            if (Generator.ValidateFullConfig(problem, group.Cfg) is { } configError)
                return new(false, $"Nhóm '{group.Name}': {configError}", output.ToString(), (int)Math.Min(int.MaxValue, totalChars));
            ulong seed = Generator.GroupSeed(baseSeed, i, group.Seed);
            string data;
            try
            {
                data = problem.Generate(new Rng(seed), group.Cfg);
                if (problem.ValidateInput(data, group.Cfg) is { } validationError)
                    return new(false, $"Nhóm '{group.Name}' sinh input không hợp lệ: {validationError}", output.ToString(), (int)Math.Min(int.MaxValue, totalChars));
            }
            catch (Exception ex)
            {
                return new(false, $"Nhóm '{group.Name}' kiểm tra thất bại: {ex.Message}", output.ToString(), (int)Math.Min(int.MaxValue, totalChars));
            }

            totalChars += data.Length;
            if (output.Length < displayLimit)
            {
                output.Append("=== ").Append(group.Name).Append(" · ").Append(group.TestCount)
                    .Append(" test · seed ").Append(Util.LL(seed)).Append(" ===\n");
                int remaining = Math.Max(0, displayLimit - output.Length);
                if (data.Length <= remaining) output.Append(data);
                else output.Append(data.AsSpan(0, remaining)).Append("\n… (đã cắt phần xem trước)");
                output.Append('\n');
            }
        }
        if (groups.Count == 0) return new(false, "Không có nhóm test để kiểm tra.", "", 0);
        if (totalChars > displayLimit) output.Append("\n… (đã giới hạn phần xem trước; mọi nhóm vẫn đã được kiểm tra)");
        return new(true, "", output.ToString(), (int)Math.Min(int.MaxValue, totalChars));
    }

    private void OnProgress(string phase, int done, int total)
    {
        if (!_running || total <= 0) return;
        barProgress.IsIndeterminate = false;
        barProgress.Maximum = total;
        barProgress.Value = done;
        lblProgress.Text = $"{phase}: {done}/{total}";
    }

    private async void BtnRun_Click(object sender, RoutedEventArgs e)
    {
        if (_model == null || _running || _reviewing) return;
        string? err = JobPlanner.Build(_model, ReadOptions(), _st, out var spec, out var policy);
        if (err != null || spec == null)
        {
            MessageBox.Show(this, err ?? "Thông tin chưa hợp lệ.", "Chưa thể tạo bộ test", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        spec.Seed ??= Generator.RandomSeed();
        var groupsForReview = spec.Groups.Count > 0
            ? spec.Groups
            : new List<TestGroupSpec> { new() { Name = "Tất cả", TestCount = spec.TestCount, Cfg = spec.Cfg } };
        _reviewing = true;
        tabMain.IsEnabled = false;
        lblStatus.Text = "Đang kiểm tra input mẫu…";
        UpdateButtons(_model.Validate() == null);
        GroupSamplePreview preflight;
        try
        {
            preflight = await Task.Run(() => CreateGroupSamples(spec.Problem, groupsForReview, spec.Seed.Value, spec.ManualTests));
        }
        catch (Exception ex)
        {
            preflight = new(false, "Lỗi khi kiểm tra input mẫu: " + ex.Message, "", 0);
        }
        finally
        {
            tabMain.IsEnabled = true;
            _reviewing = false;
            lblStatus.Text = "Sẵn sàng";
            UpdateButtons(_model.Validate() == null);
        }
        string sampleInfo = preflight.Text.Length == 0 ? "(không có input xem trước)" : preflight.Text;
        string summary = $"Bài: {(spec.ProblemName.Length == 0 ? spec.Problem.Title : spec.ProblemName)} · khuôn: {spec.Problem.Title}\n" +
                         $"Mã: {(spec.ProblemCode.Length == 0 ? spec.Problem.Id : spec.ProblemCode)} · tổng: {spec.TestCount} test ({spec.ManualTests.Count} nhập tay, {spec.GeneratedTestCount} tự sinh) · seed chính: {Util.LL(spec.Seed.Value)}\n" +
                         $"Thư mục: {spec.OutDir}\nLời giải chuẩn: {(spec.SolutionPath.Length == 0 ? "không dùng" : spec.SolutionPath)}\n" +
                         $"Đóng gói ZIP: {(spec.Zip == ZipMode.Yes ? "có" : "không")}";
        var review = new ReviewPlanWindow(summary, sampleInfo, preflight.Valid, preflight.Error) { Owner = this };
        if (review.ShowDialog() != true) return;
        if (!spec.ResumeGeneration && policy == ExistingPolicy.Overwrite && JobPlanner.OutDirHasData(spec.OutDir))
        {
            var ask = MessageBox.Show(this,
                $"Thư mục\n{spec.OutDir}\nđã có dữ liệu bộ test. Khi ghi đè, OjGen sẽ xóa các file .inp/.out, metadata của bộ test (ojgen_info.txt, SHA256SUMS.txt, checkpoint, provenance, báo cáo audit, PACKAGE.txt, PACKAGE_SHA256SUMS.txt, problem.ojdraft.json) và file .zip cùng tên. File khác sẽ được giữ lại.\n\nTiếp tục?",
                "Xác nhận ghi đè", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (ask != MessageBoxResult.Yes) return;
        }

        var st = _st;
        var log = _log;
        var job = spec;
        var res = await RunWork("Tạo bộ test", token =>
        {
            string dir = job.OutDir;
            if (job.ResumeGeneration)
            {
                try { Directory.CreateDirectory(dir); }
                catch (Exception ex) { return new JobResult { ExitCode = 2, Error = "Không tạo được thư mục tiếp tục: " + ex.Message, OutDir = dir }; }
            }
            else if (!Job.PrepareOutDir(ref dir, policy, log, out string? perr))
            {
                log.Line("LỖI: " + perr);
                return new JobResult { ExitCode = 2, Error = perr, OutDir = dir };
            }
            job.OutDir = dir;
            return Job.Run(job, st, log, token);
        });
        _lastGeneratedResult = res;
        _lastGeneratedSpec = CloneJobSpec(job);
        ShowResult(res, "Tạo bộ test");
        if (res != null && job.SolutionPath.Length > 0 && res.ExitCode != 2)
        {
            UpdateSolutionMemory(job);
            int width = FileManager.TestNumberWidth(job.TestCount);
            for (int i = 0; i < Math.Min(_manualTests.Count, job.ManualTests.Count); i++)
            {
                if (!_manualTests[i].IsSample) continue;
                string name = FileManager.TestName(_st.Prefix, i + 1, width);
                string output = Path.Combine(job.OutDir, name + ".out");
                _manualTests[i].GeneratedOutput = File.Exists(output) ? FileManager.ReadText(output) : "";
            }
        }
    }

    private void MnuQueueAdd_Click(object sender, RoutedEventArgs e)
    {
        if (_model == null || _running || _reviewing) return;
        string? error = JobPlanner.Build(_model, ReadOptions(), _st, out var spec, out var policy);
        if (error != null || spec == null)
        {
            MessageBox.Show(this, error ?? "Thông tin chưa hợp lệ.", "Không thể thêm vào hàng đợi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        spec.Seed ??= Generator.RandomSeed();
        _jobQueue.Add(new QueuedJobEntry { Spec = CloneJobSpec(spec), Policy = policy });
        _log.Line($"Đã thêm vào hàng đợi: {(spec.ProblemName.Trim().Length > 0 ? spec.ProblemName : spec.Problem.Title)}, {spec.TestCount} test, seed {Util.LL(spec.Seed.Value)}.");
        RefreshQueueMenu();
    }

    private void MnuQueueManage_Click(object sender, RoutedEventArgs e)
    {
        if (_running || _reviewing || _jobQueue.Count == 0) return;
        new QueueManagerWindow(_jobQueue, PersistQueue) { Owner = this }.ShowDialog();
        RefreshQueueMenu();
    }

    private void PersistQueue()
    {
        try
        {
            if (_jobQueue.Count == 0)
            {
                if (File.Exists(_queueStatePath)) File.Delete(_queueStatePath);
                return;
            }
            JobQueuePersistence.Save(_queueStatePath, _jobQueue);
        }
        catch (Exception ex) { _log.Line("! Không tự lưu được hàng đợi: " + ex.Message); }
    }

    private void RestoreQueue()
    {
        try
        {
            foreach (var item in JobQueuePersistence.Load(_queueStatePath)) _jobQueue.Add(item);
            if (_jobQueue.Count > 0) _log.Line($"Đã khôi phục {_jobQueue.Count} mục trong hàng đợi.");
            RefreshQueueMenu();
        }
        catch (Exception ex) { _log.Line("! Không khôi phục được hàng đợi đã lưu: " + ex.Message); }
    }

    private async void MnuQueueRun_Click(object sender, RoutedEventArgs e)
    {
        if (_running || _reviewing || _jobQueue.Count == 0) return;
        while (_jobQueue.Count > 0)
        {
            var queued = _jobQueue[0];
            JobSpec job = CloneJobSpec(queued.Spec);
            var groups = job.Groups.Count > 0 ? job.Groups : new List<TestGroupSpec>
                { new() { Name = "Tất cả", TestCount = job.TestCount, Cfg = job.Cfg } };
            _reviewing = true;
            tabMain.IsEnabled = false;
            lblStatus.Text = "Đang kiểm tra cấu hình hàng đợi…";
            UpdateButtons(_model?.Validate() == null);
            GroupSamplePreview preflight;
            try { preflight = await Task.Run(() => CreateGroupSamples(job.Problem, groups, job.Seed ?? 0, job.ManualTests)); }
            catch (Exception ex) { preflight = new(false, "Lỗi khi kiểm tra input mẫu: " + ex.Message, "", 0); }
            finally
            {
                tabMain.IsEnabled = true;
                _reviewing = false;
                lblStatus.Text = "Sẵn sàng";
                UpdateButtons(_model?.Validate() == null);
            }
            if (!preflight.Valid)
            {
                MessageBox.Show(this, preflight.Error, "Cấu hình trong hàng đợi chưa hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
                break;
            }
            string title = job.ProblemName.Trim().Length > 0 ? job.ProblemName : job.Problem.Title;
            string summary = $"Hàng đợi: {_jobQueue.Count} mục · đang chạy: {title}\nKhuôn: {job.Problem.Title} · {job.TestCount} test · seed {Util.LL(job.Seed ?? 0)}\nThư mục: {job.OutDir}";
            var review = new ReviewPlanWindow(summary, preflight.Text, true, "") { Owner = this };
            if (review.ShowDialog() != true) break;
            if (queued.Policy == ExistingPolicy.Overwrite && JobPlanner.OutDirHasData(job.OutDir))
            {
                var answer = MessageBox.Show(this, $"Thư mục\n{job.OutDir}\nđã có dữ liệu bộ test. File .inp/.out, metadata của bộ test (bao gồm bản nháp problem.ojdraft.json) và file .zip cùng tên sẽ bị xóa; file khác được giữ lại. Tiếp tục mục này?",
                    "Xác nhận hàng đợi", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (answer != MessageBoxResult.Yes) break;
            }
            JobResult? result = await RunWork($"Hàng đợi · {title}", token =>
            {
                string dir = job.OutDir;
                if (job.ResumeGeneration)
                {
                    try { Directory.CreateDirectory(dir); }
                    catch (Exception ex) { return new JobResult { ExitCode = 2, Error = "Không tạo được thư mục tiếp tục: " + ex.Message, OutDir = dir }; }
                }
                else if (!Job.PrepareOutDir(ref dir, queued.Policy, _log, out string? prepareError))
                    return new JobResult { ExitCode = 2, Error = prepareError, OutDir = dir };
                job.OutDir = dir;
                return Job.Run(job, _st, _log, token);
            });
            if (result == null) break;
            bool completed = !result.Cancelled && result.ExitCode == 0;
            if (completed) _jobQueue.Remove(queued);
            _lastGeneratedResult = result;
            _lastGeneratedSpec = CloneJobSpec(job);
            ShowResult(result, "Hàng đợi");
            RefreshQueueMenu();
            if (!completed) break;
        }
        RefreshQueueMenu();
    }

    private static JobSpec CloneJobSpec(JobSpec source) => new()
    {
        Problem = source.Problem,
        Cfg = source.Cfg.Clone(),
        TestCount = source.TestCount,
        GeneratedTestCount = source.GeneratedTestCount,
        Seed = source.Seed,
        SolutionPath = source.SolutionPath,
        OutDir = source.OutDir,
        Zip = source.Zip,
        ZipIncomplete = source.ZipIncomplete,
        ForceRebuild = source.ForceRebuild,
        ResumeGeneration = source.ResumeGeneration,
        ProblemName = source.ProblemName,
        ProblemCode = source.ProblemCode,
        Groups = source.Groups.Select(group => group.Clone()).ToList(),
        RequiredCoverageTags = source.RequiredCoverageTags.ToList(),
        ManualTests = source.ManualTests.Select(test => test.Clone()).ToList(),
    };

    private void UpdateSolutionMemory(JobSpec job)
    {
        // Job đã ghi nhớ lời giải vào file cấu hình; đồng bộ lại bản trong bộ nhớ.
        _st.Solution = job.SolutionPath;
        _st.SolutionByProblem[job.Problem.Id] = job.SolutionPath;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        lblStatus.Text = "Đang hủy…";
        btnCancel.IsEnabled = false;
    }

    private void ShowResult(JobResult? r, string what)
    {
        if (r == null)
        {
            SetResultText("❌ " + what + " thất bại do lỗi không mong đợi. Xem tab Nhật ký.", "ErrorBrush", false, false);
            return;
        }
        bool hasDir = r.OutDir.Length > 0 && Directory.Exists(r.OutDir);
        bool hasZip = r.ZipPath != null && File.Exists(r.ZipPath);
        if (r.Cancelled)
            SetResultText("⏹ Đã hủy. " + (r.InputCount > 0 ? $"Đã tạo {r.InputCount} file .inp (chưa đủ bộ)." : ""), "WarningBrush", hasDir, hasZip, r.Coverage);
        else if (r.ExitCode == 2)
            SetResultText("❌ Lỗi: " + (r.Error ?? "xem tab Nhật ký"), "ErrorBrush", hasDir, hasZip);
        else if (r.Status == SetStatus.Complete)
        {
            int ok = r.Report?.Ok ?? 0;
            SetResultText($"✅ Hoàn chỉnh: {r.InputCount} test, {ok} file .out. Seed {Util.LL(r.Seed)}." + (hasZip ? " Đã nén ZIP." : ""),
                "SuccessBrush", hasDir, hasZip, r.Coverage);
        }
        else if (r.Status == SetStatus.InputOnly)
            SetResultText($"✅ Đã tạo {r.InputCount} file .inp (chưa có .out vì không dùng lời giải). Seed {Util.LL(r.Seed)}." + (hasZip ? " Đã nén ZIP." : ""),
                "SuccessBrush", hasDir, hasZip, r.Coverage);
        else
        {
            int fails = r.Report?.Failures.Count ?? 0;
            string detail = fails > 0 ? $"{fails}/{r.Report!.Total} test lỗi" : "lời giải không chạy được";
            SetResultText($"⚠ Chưa hoàn chỉnh: {detail}. Xem tab Nhật ký để biết lý do.", "WarningBrush", hasDir, hasZip, r.Coverage);
        }
    }

    private void SetResultText(string text, string brushKey, bool showFolder, bool showZip, CoverageReport? coverage = null)
    {
        lblResult.Text = text;
        lblResult.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        btnCoverage.Visibility = coverage == null ? Visibility.Collapsed : Visibility.Visible;
        _lastCoverage = coverage;
        btnOpenFolder.Visibility = showFolder ? Visibility.Visible : Visibility.Collapsed;
        btnOpenZip.Visibility = showZip ? Visibility.Visible : Visibility.Collapsed;
        resultPanel.Visibility = Visibility.Visible;
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult != null) OpenPath(_lastResult.OutDir);
    }

    private CoverageReport? _lastCoverage;

    private void BtnCoverage_Click(object sender, RoutedEventArgs e)
    {
        if (_lastCoverage == null) return;
        new CoverageWindow(_lastCoverage) { Owner = this }.ShowDialog();
    }

    private void BtnOpenZip_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult?.ZipPath is { } zip && File.Exists(zip)) RevealInExplorer(zip);
    }

    private void BtnShowLog_Click(object sender, RoutedEventArgs e) => tabMain.SelectedIndex = 1;

    private static void OpenPath(string path)
    {
        try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show("Không mở được: " + path + "\n" + ex.Message); }
    }

    private static void RevealInExplorer(string file)
    {
        try { Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{file}\"", UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show("Không mở được: " + file + "\n" + ex.Message); }
    }

    // ============================================================ NHẬT KÝ

    private void AppendLog(string chunk)
    {
        txtLog.AppendText(chunk);
        if (txtLog.Text.Length > LogMaxChars)
            txtLog.Text = "… (đã cắt bớt phần đầu nhật ký)\n" + txtLog.Text[^(LogMaxChars / 2)..];
        txtLog.ScrollToEnd();
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e) => txtLog.Clear();

    private void BtnCopyLog_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(txtLog.Text); lblStatus.Text = "Đã sao chép nhật ký"; }
        catch (Exception ex) { MessageBox.Show(this, "Không sao chép được: " + ex.Message); }
    }

    private void BtnSaveLog_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Title = "Lưu nhật ký", Filter = "Văn bản (*.txt)|*.txt", FileName = "ojgen_log.txt" };
        if (dlg.ShowDialog(this) != true) return;
        try { AtomicFile.WriteAllText(dlg.FileName, txtLog.Text, new UTF8Encoding(false)); }
        catch (Exception ex) { MessageBox.Show(this, "Không lưu được: " + ex.Message); }
    }

    // ============================================================ MENU

    private void MnuOpenOutput_Click(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(_st.OutputRoot); } catch (Exception) { }
        OpenPath(_st.OutputRoot);
    }

    private void MnuExit_Click(object sender, RoutedEventArgs e) => Close();

    private async void MnuRegen_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        string sol = txtSolution.Text.Trim().Trim('"');
        if (sol.Length == 0)
        {
            MessageBox.Show(this, "Hãy chọn lời giải chuẩn ở tab 'Tạo bộ test' trước.", "Thiếu lời giải", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục đã có file .inp" };
        if (dlg.ShowDialog(this) != true) return;
        string dir = dlg.FolderName;
        var st = _st;
        var log = _log;
        var opt = ReadOptions();
        var res = await RunWork("Tạo lại output", token =>
            Job.RegenerateOutputs(dir, sol, st, opt.Zip ? ZipMode.Yes : ZipMode.No, opt.Rebuild, opt.ZipIncomplete, log, token));
        ShowResult(res, "Tạo lại output");
        tabMain.SelectedIndex = 0;
    }

    private async void MnuZipDir_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục test cần nén" };
        if (dlg.ShowDialog(this) != true) return;
        string dir = dlg.FolderName;
        var st = _st;
        var log = _log;
        var res = await RunWork("Nén ZIP", _ =>
        {
            bool ok = Job.ZipTestFolder(dir, st, log, out string? zip);
            return new JobResult
            {
                ExitCode = ok ? 0 : 2,
                Status = SetStatus.InputOnly,
                OutDir = dir,
                ZipPath = zip,
                InputCount = FileManager.ListFilesWithExt(dir, ".inp").Count,
                Error = ok ? null : "không tạo được ZIP",
            };
        });
        if (res == null) return;
        if (res.ExitCode == 0) SetResultText("📦 Đã nén ZIP: " + res.ZipPath, "SuccessBrush", true, true);
        else SetResultText("❌ Không tạo được ZIP. Xem tab Nhật ký.", "ErrorBrush", true, false);
    }

    private void MnuInventory_Click(object sender, RoutedEventArgs e)
    {
        string directory = _lastGeneratedResult?.OutDir ?? _lastGeneratedSpec?.OutDir ?? "";
        if (!Directory.Exists(directory))
        {
            var dialog = new OpenFolderDialog { Title = "Chọn thư mục test cần kiểm kê" };
            if (dialog.ShowDialog(this) != true) return;
            directory = dialog.FolderName;
        }
        new TestInventoryWindow(directory, _model?.Problem, _model?.Effective()) { Owner = this }.ShowDialog();
    }

    private void MnuSolutionMatrix_Click(object sender, RoutedEventArgs e)
    {
        string directory = _lastGeneratedResult?.OutDir ?? _lastGeneratedSpec?.OutDir ?? "";
        if (!Directory.Exists(directory))
        {
            var dialog = new OpenFolderDialog { Title = "Chọn thư mục test để so sánh nhiều lời giải" };
            if (dialog.ShowDialog(this) != true) return;
            directory = dialog.FolderName;
        }
        new SolutionMatrixWindow(directory, _st) { Owner = this }.ShowDialog();
    }

    private async void MnuSelfTest_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        tabMain.SelectedIndex = 1;
        var log = _log;
        var res = await RunWork("Kiểm tra nội bộ", _ => new JobResult { ExitCode = SelfTest.Run(log) });
        if (res == null) return;
        MessageBox.Show(this,
            res.ExitCode == 0 ? "SELFTEST: ĐẠT — mọi kiểm tra đều thành công." : "SELFTEST: KHÔNG ĐẠT — xem chi tiết ở tab Nhật ký.",
            "Kiểm tra nội bộ", MessageBoxButton.OK, res.ExitCode == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void MnuSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        var dlg = new SettingsWindow(_st) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            UpdateStatusBar();
            if (cmbProblem.SelectedItem is Problem p)
                lblOutHint.Text = "Để trống thư mục đầu ra = " + JobPlanner.DefaultOutDir(p, _st, _problemCode);
        }
    }

    private void MnuToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        ThemeService.Toggle();
        UpdateStatusBar();
    }

    private void MnuAutoTheme_Click(object sender, RoutedEventArgs e)
    {
        bool on = mnuAutoTheme.IsChecked;
        AppRegistry.SetBool("AutoThemeEnabled", on);
        if (on) AutoThemeService.Start();
        else AutoThemeService.Stop();
        UpdateAutoThemeHeader();
        UpdateStatusBar();
    }

    // Template MenuItem của TQLTK không vẽ dấu tick, nên hiện trạng thái bật/tắt ngay trong tiêu đề.
    private void UpdateAutoThemeHeader() =>
        mnuAutoTheme.Header = "⏰ Tự động đổi theme theo giờ" + (mnuAutoTheme.IsChecked ? "  ✔" : "");

    private void MnuCenterWindow_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Normal;
        var area = SystemParameters.WorkArea;
        Left = area.Left + Math.Max(0, (area.Width - Width) / 2);
        Top = area.Top + Math.Max(0, (area.Height - Height) / 2);
    }

    private void MnuHelp_Click(object sender, RoutedEventArgs e) => tabMain.SelectedIndex = 2;

    private void MnuAbout_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this,
            $"OjGen {Util.Version}\nTrình tạo bộ test cho bài lập trình thi đấu.\n\n" +
            "Chuyển từ bản C++ (Trinh-tao-contest-Classic-) sang C#/.NET, giao diện WPF theo phong cách TQLTK.\n" +
            "Cùng seed + cùng cấu hình cho ra bộ test giống hệt bản C++.",
            "Giới thiệu", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void UpdateStatusBar()
    {
        lblTheme.Text = (ThemeService.IsDarkMode ? "Giao diện: Tối" : "Giao diện: Sáng") +
                        (AutoThemeService.IsEnabled ? " (tự động)" : "");
        lblRoot.Text = "Output: " + _st.OutputRoot;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_running)
        {
            var ask = MessageBox.Show(this, "Đang có công việc chạy. Hủy công việc và thoát?", "Thoát OjGen",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (ask != MessageBoxResult.Yes) { e.Cancel = true; return; }
            _cts?.Cancel();
        }
        _projectAutosaveTimer.Stop();
        SaveProjectSnapshot(_projectAutosavePath, quiet: true, force: true);
        base.OnClosing(e);
    }
}
