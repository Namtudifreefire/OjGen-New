using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    private JobResult? _lastResult;
    private bool _loading = true;

    public MainWindow()
    {
        InitializeComponent();
        _st = ((App)Application.Current).AppSettings;

        _log = new UiLog(Dispatcher);
        _log.TextAppended += AppendLog;
        _log.ProgressChanged += OnProgress;

        txtHelp.Text = HelpContent.Text;
        cmbProblem.ItemsSource = Problems.All;

        string last = AppRegistry.GetString("LastProblem", "");
        cmbProblem.SelectedItem = Problems.Find(last) ?? Problems.All[0];

        mnuAutoTheme.IsChecked = AutoThemeService.IsEnabled;
        UpdateAutoThemeHeader();
        UpdateStatusBar();
        _loading = false;

        string? warn = ((App)Application.Current).SettingsWarning;
        if (!string.IsNullOrEmpty(warn)) _log.Line(warn);
    }

    // ============================================================ DẠNG BÀI / FORM

    private void CmbProblem_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbProblem.SelectedItem is not Problem p) return;
        _model = new ConfigModel(p);
        _form = new ParamForm(_model, paramHost);
        _form.Changed += OnFormChanged;

        txtFormat.Text = p.FormatDescription.TrimEnd();
        string sol = _st.SolutionFor(p.Id);
        txtSolution.Text = sol.Length > 0 ? sol : _st.Solution;
        lblOutHint.Text = "Để trống thư mục đầu ra = " + JobPlanner.DefaultOutDir(p, _st);
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
        btnRun.IsEnabled = !_running && formValid;
        btnPreview.IsEnabled = !_running && formValid;
        btnCancel.IsEnabled = _running;
        cmbProblem.IsEnabled = !_running;
        mnuSettings.IsEnabled = !_running;
        mnuRegen.IsEnabled = !_running;
        mnuZipDir.IsEnabled = !_running;
        mnuSelfTest.IsEnabled = !_running;
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
    };

    // ============================================================ CHỌN FILE / THƯ MỤC

    private void BtnRandomSeed_Click(object sender, RoutedEventArgs e) =>
        txtSeed.Text = Util.LL(Generator.RandomSeed());

    private void BtnBrowseSolution_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Chọn lời giải chuẩn",
            Filter = "Lời giải (*.cpp;*.cc;*.cxx;*.py;*.exe)|*.cpp;*.cc;*.cxx;*.c++;*.py;*.exe|Tất cả file (*.*)|*.*",
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
        if (_model == null || _running) return;
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

        var cfg = _model.Effective();
        var prob = _model.Problem;
        btnPreview.IsEnabled = false;
        lblPreviewInfo.Text = "Đang sinh dữ liệu…";
        try
        {
            string data = await Task.Run(() => prob.Generate(new Rng(seed), cfg));
            string shown = data;
            if (data.Length > PreviewMaxChars)
                shown = data[..PreviewMaxChars] + $"\n… (đã cắt, còn {Util.LL(data.Length - PreviewMaxChars)} ký tự nữa)";
            txtPreview.Text = shown;
            int lines = data.Count(c => c == '\n');
            lblPreviewInfo.Text = $"Test mẫu: {Util.FmtBytes((ulong)data.Length)}, {lines} dòng. " +
                (randomSeed ? $"Seed ngẫu nhiên vừa dùng: {Util.LL(seed)} (nhập vào ô Seed để tạo đúng test này làm test01)."
                            : $"Seed {Util.LL(seed)} — đây chính là test01 sẽ được tạo.");
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
        if (_model == null || _running) return;
        string? err = JobPlanner.Build(_model, ReadOptions(), _st, out var spec, out var policy);
        if (err != null || spec == null)
        {
            MessageBox.Show(this, err ?? "Thông tin chưa hợp lệ.", "Chưa thể tạo bộ test", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (policy == ExistingPolicy.Overwrite && JobPlanner.OutDirHasData(spec.OutDir))
        {
            var ask = MessageBox.Show(this,
                $"Thư mục\n{spec.OutDir}\nđã có test. Các file .inp, .out, ojgen_info.txt và file .zip cùng tên sẽ bị XÓA rồi tạo lại.\n\nTiếp tục?",
                "Xác nhận ghi đè", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (ask != MessageBoxResult.Yes) return;
        }

        var st = _st;
        var log = _log;
        var job = spec;
        var res = await RunWork("Tạo bộ test", token =>
        {
            string dir = job.OutDir;
            if (!Job.PrepareOutDir(ref dir, policy, log, out string? perr))
            {
                log.Line("LỖI: " + perr);
                return new JobResult { ExitCode = 2, Error = perr, OutDir = dir };
            }
            job.OutDir = dir;
            return Job.Run(job, st, log, token);
        });
        ShowResult(res, "Tạo bộ test");
        if (res != null && job.SolutionPath.Length > 0 && res.ExitCode != 2) UpdateSolutionMemory(job);
    }

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
            SetResultText("⏹ Đã hủy. " + (r.InputCount > 0 ? $"Đã tạo {r.InputCount} file .inp (chưa đủ bộ)." : ""), "WarningBrush", hasDir, hasZip);
        else if (r.ExitCode == 2)
            SetResultText("❌ Lỗi: " + (r.Error ?? "xem tab Nhật ký"), "ErrorBrush", hasDir, hasZip);
        else if (r.Status == SetStatus.Complete)
        {
            int ok = r.Report?.Ok ?? 0;
            SetResultText($"✅ Hoàn chỉnh: {r.InputCount} test, {ok} file .out. Seed {Util.LL(r.Seed)}." + (hasZip ? " Đã nén ZIP." : ""),
                "SuccessBrush", hasDir, hasZip);
        }
        else if (r.Status == SetStatus.InputOnly)
            SetResultText($"✅ Đã tạo {r.InputCount} file .inp (chưa có .out vì không dùng lời giải). Seed {Util.LL(r.Seed)}." + (hasZip ? " Đã nén ZIP." : ""),
                "SuccessBrush", hasDir, hasZip);
        else
        {
            int fails = r.Report?.Failures.Count ?? 0;
            string detail = fails > 0 ? $"{fails}/{r.Report!.Total} test lỗi" : "lời giải không chạy được";
            SetResultText($"⚠ Chưa hoàn chỉnh: {detail}. Xem tab Nhật ký để biết lý do.", "WarningBrush", hasDir, hasZip);
        }
    }

    private void SetResultText(string text, string brushKey, bool showFolder, bool showZip)
    {
        lblResult.Text = text;
        lblResult.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        btnOpenFolder.Visibility = showFolder ? Visibility.Visible : Visibility.Collapsed;
        btnOpenZip.Visibility = showZip ? Visibility.Visible : Visibility.Collapsed;
        resultPanel.Visibility = Visibility.Visible;
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult != null) OpenPath(_lastResult.OutDir);
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
        try { File.WriteAllText(dlg.FileName, txtLog.Text, new UTF8Encoding(false)); }
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
                lblOutHint.Text = "Để trống thư mục đầu ra = " + JobPlanner.DefaultOutDir(p, _st);
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
        base.OnClosing(e);
    }
}
