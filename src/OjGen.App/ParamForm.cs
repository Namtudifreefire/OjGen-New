using System.Windows;
using System.Windows.Controls;
using OjGen.Core;

namespace OjGen.Gui;

/// <summary>
/// Dựng form nhập tham số từ Problem.Params (số nguyên, bool, văn bản, danh sách lựa chọn) và nối với ConfigModel.
/// Mọi logic kiểm tra/ẩn-hiện/đồng bộ nằm ở ConfigModel (đã kiểm thử); lớp này chỉ ánh xạ controls.
/// </summary>
internal sealed class ParamForm
{
    private sealed class RowUi
    {
        public required ParamSpec Spec { get; init; }
        public required StackPanel Panel { get; init; }
        public TextBox? Text { get; set; }
        public ComboBox? Combo { get; set; }
        public CheckBox? Check { get; set; }
    }

    private readonly ConfigModel _model;
    private readonly Dictionary<string, RowUi> _rows = new();
    private bool _syncing;

    /// <summary>Phát ra mỗi khi giá trị hoặc trạng thái hợp lệ thay đổi.</summary>
    public event Action? Changed;

    public ConfigModel Model => _model;

    public ParamForm(ConfigModel model, Panel host)
    {
        _model = model;
        host.Children.Clear();
        foreach (var spec in model.Problem.Params)
        {
            var row = BuildRow(spec);
            _rows[spec.Key] = row;
            host.Children.Add(row.Panel);
        }
        RefreshAll();
    }

    // ------------------------------------------------------------ dựng từng dòng

    private RowUi BuildRow(ParamSpec spec)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 2) };
        var row = new RowUi { Spec = spec, Panel = panel };

        if (spec.Type == ParamType.Bool)
        {
            var chk = new CheckBox
            {
                Content = new TextBlock { Text = spec.Label, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0) },
                Margin = new Thickness(3, 10, 3, 2),
                IsChecked = _model.Config.I(spec.Key) != 0,
                ToolTip = "--" + spec.Key,
            };
            chk.Checked += (_, _) => OnCheckChanged(row);
            chk.Unchecked += (_, _) => OnCheckChanged(row);
            row.Check = chk;
            panel.Children.Add(chk);
        }
        else
        {
            panel.Children.Add(new TextBlock
            {
                Text = spec.DisplayLabel,
                Style = (Style)Application.Current.FindResource("FieldLabel"),
                TextWrapping = TextWrapping.Wrap,
                ToolTip = "--" + spec.Key,
            });

            if (spec.Type == ParamType.Int && spec.Options is { Count: > 0 })
            {
                var cb = new ComboBox
                {
                    ItemsSource = spec.Options,
                    DisplayMemberPath = nameof(ParamOption.Label),
                    Margin = new Thickness(0, 3, 0, 3),
                    ToolTip = "--" + spec.Key,
                };
                cb.SelectedItem = spec.Options.FirstOrDefault(o => o.Value == _model.Config.I(spec.Key));
                cb.SelectionChanged += (_, _) => OnComboChanged(row);
                row.Combo = cb;
                panel.Children.Add(cb);
            }
            else
            {
                var tb = new TextBox
                {
                    Text = _model.DisplayValue(spec),
                    ToolTip = "--" + spec.Key,
                };
                if (spec.Multiline)
                {
                    tb.AcceptsReturn = true;
                    tb.TextWrapping = TextWrapping.Wrap;
                    tb.MinHeight = 96;
                    tb.VerticalContentAlignment = VerticalAlignment.Top;
                }
                tb.TextChanged += (_, _) => OnTextChanged(row);
                row.Text = tb;
                panel.Children.Add(tb);

                if (spec.Type == ParamType.Text && spec.Choices is { Count: > 0 })
                {
                    var chips = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
                    foreach (var choice in spec.Choices)
                    {
                        string value = choice;
                        var btn = new Button
                        {
                            Content = value,
                            Style = (Style)Application.Current.FindResource("OutlineButton"),
                            Padding = new Thickness(8, 2, 8, 2),
                            FontSize = 11,
                            Margin = new Thickness(0, 2, 4, 2),
                        };
                        btn.Click += (_, _) => tb.Text = value;
                        chips.Children.Add(btn);
                    }
                    panel.Children.Add(chips);
                }
            }

            string hint = BuildHint(spec);
            if (hint.Length > 0)
                panel.Children.Add(new TextBlock { Text = hint, Style = (Style)Application.Current.FindResource("HintText") });
        }
        return row;
    }

    private static string BuildHint(ParamSpec spec)
    {
        var parts = new List<string>();
        if (spec.Help.Length > 0) parts.Add(spec.Help);
        if (spec.Type == ParamType.Int && spec.Options == null && (spec.Lo > long.MinValue || spec.Hi < long.MaxValue))
            parts.Add($"Miền cho phép: {Util.LL(spec.Lo)} .. {Util.LL(spec.Hi)}");
        else if (spec.Type == ParamType.Int && spec.Options == null)
            parts.Add("Nhập số nguyên 64-bit; hỗ trợ dạng 1e9, 2e5");
        return string.Join("\n", parts);
    }

    // ------------------------------------------------------------ sự kiện

    private void OnTextChanged(RowUi row)
    {
        if (_syncing || row.Text == null) return;
        var changed = row.Spec.Type == ParamType.Int
            ? _model.EditInt(row.Spec.Key, row.Text.Text)
            : _model.EditText(row.Spec.Key, row.Text.Text);
        AfterEdit(changed);
    }

    private void OnComboChanged(RowUi row)
    {
        if (_syncing || row.Combo?.SelectedItem is not ParamOption opt) return;
        AfterEdit(_model.SetInt(row.Spec.Key, opt.Value));
    }

    private void OnCheckChanged(RowUi row)
    {
        if (_syncing || row.Check == null) return;
        AfterEdit(_model.SetInt(row.Spec.Key, row.Check.IsChecked == true ? 1 : 0));
    }

    private void AfterEdit(IReadOnlyList<string> changedOthers)
    {
        foreach (var key in changedOthers) SyncEditor(key);
        RefreshAll();
        Changed?.Invoke();
    }

    // ------------------------------------------------------------ đồng bộ giao diện

    private void SyncEditor(string key)
    {
        if (!_rows.TryGetValue(key, out var row)) return;
        _syncing = true;
        try
        {
            if (row.Text != null) row.Text.Text = _model.DisplayValue(row.Spec);
            if (row.Combo != null)
                row.Combo.SelectedItem = row.Spec.Options?.FirstOrDefault(o => o.Value == _model.Config.I(key));
            if (row.Check != null) row.Check.IsChecked = _model.Config.I(key) != 0;
        }
        finally { _syncing = false; }
    }

    private void RefreshAll()
    {
        foreach (var row in _rows.Values)
        {
            row.Panel.Visibility = _model.IsVisible(row.Spec) ? Visibility.Visible : Visibility.Collapsed;
            if (row.Text != null && row.Spec.Type == ParamType.Int)
            {
                if (_model.FieldError(row.Spec.Key) != null)
                    row.Text.SetResourceReference(Control.BorderBrushProperty, "ErrorBrush");
                else
                    row.Text.ClearValue(Control.BorderBrushProperty);
            }
        }
    }
}
