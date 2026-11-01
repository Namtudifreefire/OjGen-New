using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using OjGen.Core;

namespace OjGen.Gui;

public partial class QueueManagerWindow : Window
{
    private readonly ObservableCollection<QueuedJobEntry> _entries;
    private readonly Action _save;

    internal QueueManagerWindow(ObservableCollection<QueuedJobEntry> entries, Action save)
    {
        InitializeComponent();
        _entries = entries; _save = save;
        lstQueue.ItemsSource = _entries;
        if (_entries.Count > 0) lstQueue.SelectedIndex = 0;
    }
    private void LstQueue_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (lstQueue.SelectedItem is QueuedJobEntry entry)
        {
            txtSeed.Text = Util.LL(entry.Spec.Seed ?? 0);
            txtOutDir.Text = entry.Spec.OutDir;
            lblInfo.Text = $"{entry.Spec.Problem.Title} · {entry.Spec.TestCount} tổng test · {entry.Spec.ManualTests.Count} test nhập tay";
        }
        else { txtSeed.Clear(); txtOutDir.Clear(); lblInfo.Text = "Hàng đợi trống."; }
    }
    private void Move(int delta)
    {
        int from = lstQueue.SelectedIndex, to = from + delta;
        if (from < 0 || to < 0 || to >= _entries.Count) return;
        _entries.Move(from, to); lstQueue.SelectedIndex = to; _save();
    }
    private void BtnMoveUp_Click(object sender, RoutedEventArgs e) => Move(-1);
    private void BtnMoveDown_Click(object sender, RoutedEventArgs e) => Move(1);
    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        int index = lstQueue.SelectedIndex;
        if (index < 0) return;
        _entries.RemoveAt(index);
        if (_entries.Count > 0) lstQueue.SelectedIndex = Math.Min(index, _entries.Count - 1);
        _save();
    }
    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        if (lstQueue.SelectedItem is not QueuedJobEntry entry) return;
        if (!Util.ParseULL(txtSeed.Text.Trim(), out ulong seed)) { lblInfo.Text = "Seed phải là số nguyên không âm."; return; }
        if (txtOutDir.Text.Trim().Length == 0) { lblInfo.Text = "Thư mục output không được để trống."; return; }
        entry.Spec.Seed = seed;
        entry.Spec.OutDir = Path.GetFullPath(txtOutDir.Text.Trim());
        _save();
        lblInfo.Text = "Đã cập nhật mục hàng đợi.";
    }
    private void BtnEditPlan_Click(object sender, RoutedEventArgs e)
    {
        if (lstQueue.SelectedItem is not QueuedJobEntry entry) return;
        var spec = entry.Spec;
        var groups = spec.Groups.Count > 0 ? spec.Groups : new List<TestGroupSpec> { new() { Name = "Tất cả", TestCount = Math.Max(1, spec.TestCount - spec.ManualTests.Count), Cfg = spec.Cfg.Clone() } };
        var dialog = new TestPlanWindow(spec.Problem, spec.Cfg, groups.Sum(group => group.TestCount), groups, spec.RequiredCoverageTags) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        spec.Groups = dialog.Groups.Select(group => group.Clone()).ToList();
        spec.RequiredCoverageTags = dialog.RequiredCoverageTags.ToList();
        spec.GeneratedTestCount = spec.Groups.Sum(group => group.TestCount);
        spec.TestCount = spec.GeneratedTestCount + spec.ManualTests.Count;
        spec.Cfg = spec.Groups.First().Cfg.Clone();
        _save();
        lblInfo.Text = "Đã cập nhật cấu hình nhóm cho mục hàng đợi.";
    }
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
