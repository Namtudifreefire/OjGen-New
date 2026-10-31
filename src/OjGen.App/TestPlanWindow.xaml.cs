using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using OjGen.Core;

namespace OjGen.Gui;

public partial class TestPlanWindow : Window
{
    private readonly ObservableCollection<TestGroupSpec> _groups = new();
    private readonly Problem _problem;
    private readonly Config _baseConfig;
    private TestGroupSpec? _current;
    private ConfigModel _model;
    private ParamForm _form;
    private bool _switching;

    public IReadOnlyList<TestGroupSpec> Groups { get; private set; } = Array.Empty<TestGroupSpec>();

    public TestPlanWindow(Problem problem, Config baseConfig, int defaultCount, IReadOnlyList<TestGroupSpec> groups)
    {
        InitializeComponent();
        _problem = problem;
        _baseConfig = baseConfig.Clone();
        if (groups.Count > 0)
            foreach (var group in groups) _groups.Add(group.Clone());
        else
            _groups.Add(new TestGroupSpec { Name = "Ngẫu nhiên", TestCount = Math.Clamp(defaultCount, 1, Generator.MaxTests), Cfg = baseConfig.Clone() });

        _model = new ConfigModel(problem, baseConfig);
        _form = new ParamForm(_model, paramHost);
        _form.Changed += OnModelChanged;
        lstGroups.ItemsSource = _groups;
        _switching = true;
        lstGroups.SelectedIndex = 0;
        _switching = false;
        _current = _groups[0];
        LoadGroup(_current);
    }

    private void OnModelChanged() => UpdateStatus();

    private bool StoreCurrent()
    {
        if (_current == null) return true;
        if (_model.Validate() is { } configError)
        {
            lblError.Text = "⚠ " + configError;
            return false;
        }
        string name = txtName.Text.Trim();
        if (name.Length == 0)
        {
            lblError.Text = "⚠ Tên nhóm không được để trống.";
            return false;
        }
        if (_groups.Any(group => !ReferenceEquals(group, _current) && string.Equals(group.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
        {
            lblError.Text = "⚠ Tên nhóm đã được dùng; hãy chọn tên khác.";
            return false;
        }
        if (!Util.ParseLL(txtCount.Text, out long count) || count < 1 || count > Generator.MaxTests)
        {
            lblError.Text = $"⚠ Số test phải trong [1, {Generator.MaxTests}].";
            return false;
        }
        ulong? seed = null;
        if (txtSeed.Text.Trim().Length > 0)
        {
            if (!Util.ParseULL(txtSeed.Text, out ulong value))
            {
                lblError.Text = "⚠ Seed riêng phải là số nguyên không âm; để trống để OjGen tự tạo.";
                return false;
            }
            seed = value;
        }
        _current.Name = name;
        _current.TestCount = (int)count;
        _current.Seed = seed;
        _current.Cfg = _model.Effective();
        lstGroups.Items.Refresh();
        UpdateStatus();
        lblError.Text = "";
        return true;
    }

    private void LoadGroup(TestGroupSpec group)
    {
        _current = group;
        _switching = true;
        txtName.Text = group.Name;
        txtCount.Text = group.TestCount.ToString();
        txtSeed.Text = group.Seed is { } seed ? Util.LL(seed) : "";
        _model = new ConfigModel(_problem, group.Cfg);
        _form = new ParamForm(_model, paramHost);
        _form.Changed += OnModelChanged;
        _switching = false;
        UpdateStatus();
        lblError.Text = "";
    }

    private void LstGroups_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_switching || lstGroups.SelectedItem is not TestGroupSpec next || ReferenceEquals(next, _current)) return;
        if (!StoreCurrent())
        {
            _switching = true;
            lstGroups.SelectedItem = _current;
            _switching = false;
            return;
        }
        LoadGroup(next);
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (!StoreCurrent()) return;
        if (_groups.Count >= Generator.MaxGroups)
        {
            lblError.Text = $"Chỉ hỗ trợ tối đa {Generator.MaxGroups} nhóm.";
            return;
        }
        var group = new TestGroupSpec
        {
            Name = $"Nhóm {_groups.Count + 1}",
            TestCount = 10,
            Cfg = (_current?.Cfg ?? _baseConfig).Clone(),
        };
        _groups.Add(group);
        lstGroups.SelectedItem = group;
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        if (_groups.Count <= 1)
        {
            lblError.Text = "Cần giữ lại ít nhất một nhóm test.";
            return;
        }
        int index = lstGroups.SelectedIndex;
        _switching = true;
        _groups.Remove(_current);
        lstGroups.SelectedIndex = Math.Min(index, _groups.Count - 1);
        _switching = false;
        LoadGroup((TestGroupSpec)lstGroups.SelectedItem);
    }

    private void BtnClone_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null || !StoreCurrent()) return;
        if (_groups.Count >= Generator.MaxGroups) { lblError.Text = $"Chỉ hỗ trợ tối đa {Generator.MaxGroups} nhóm."; return; }
        string baseName = _current.Name + " (bản sao)";
        string name = baseName;
        int suffix = 2;
        while (_groups.Any(group => string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase)))
            name = baseName + " " + suffix++;
        var clone = _current.Clone();
        clone.Name = name;
        int index = lstGroups.SelectedIndex;
        _groups.Insert(index + 1, clone);
        lstGroups.SelectedItem = clone;
    }

    private void MoveCurrent(int delta)
    {
        if (_current == null || !StoreCurrent()) return;
        int index = lstGroups.SelectedIndex;
        int target = index + delta;
        if (target < 0 || target >= _groups.Count) return;
        _groups.Move(index, target);
        lstGroups.SelectedItem = _groups[target];
    }

    private void BtnMoveUp_Click(object sender, RoutedEventArgs e) => MoveCurrent(-1);
    private void BtnMoveDown_Click(object sender, RoutedEventArgs e) => MoveCurrent(1);

    private void BtnSavePlan_Click(object sender, RoutedEventArgs e)
    {
        if (!StoreCurrent()) return;
        long total = _groups.Sum(group => (long)group.TestCount);
        if (total > Generator.MaxTests) { lblError.Text = $"Tổng số test không được vượt quá {Generator.MaxTests}."; return; }
        var dialog = new SaveFileDialog
        {
            Title = "Lưu kế hoạch nhóm test",
            Filter = "OjGen test plan (*.ojplan.json)|*.ojplan.json|JSON (*.json)|*.json",
            DefaultExt = ".ojplan.json",
            AddExtension = true,
            FileName = _problem.Id + ".ojplan.json",
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            TestPlanProfile.From(_problem, _groups).Save(dialog.FileName);
            lblError.Text = "Kế hoạch đã được lưu.";
        }
        catch (Exception ex) { lblError.Text = "Không lưu được kế hoạch: " + ex.Message; }
    }

    private void BtnLoadPlan_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Mở kế hoạch nhóm test",
            Filter = "OjGen test plan (*.ojplan.json)|*.ojplan.json|JSON (*.json)|*.json",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var profile = TestPlanProfile.Load(dialog.FileName);
            var loaded = profile.ToGroups(_problem);
            _switching = true;
            _groups.Clear();
            foreach (var group in loaded) _groups.Add(group);
            _current = null;
            lstGroups.SelectedIndex = 0;
            _switching = false;
            LoadGroup(_groups[0]);
            lblError.Text = $"Đã nạp {loaded.Count} nhóm, tổng {profile.TotalTestCount} test.";
        }
        catch (Exception ex) { lblError.Text = "Không nạp được kế hoạch: " + ex.Message; }
    }

    private void UpdateStatus()
    {
        long total = 0;
        foreach (var group in _groups) total += group.TestCount;
        if (_current != null && Util.ParseLL(txtCount.Text, out long editingCount) && editingCount > 0)
            total += editingCount - _current.TestCount;
        lblTotal.Text = $"Tổng: {Math.Max(0, total)} test";
    }

    private void Draft_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_switching) UpdateStatus();
    }

    private void BtnFinish_Click(object sender, RoutedEventArgs e)
    {
        if (!StoreCurrent()) return;
        long total = _groups.Sum(group => (long)group.TestCount);
        if (total > Generator.MaxTests)
        {
            lblError.Text = $"Tổng số test không được vượt quá {Generator.MaxTests}.";
            return;
        }
        Groups = _groups.Select(group => group.Clone()).ToList();
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
