using System.Windows;
using Microsoft.Win32;
using OjGen.Core;

namespace OjGen.Gui;

public partial class SettingsWindow : Window
{
    private readonly Settings _st;

    public SettingsWindow(Settings st)
    {
        InitializeComponent();
        _st = st;

        txtCompiler.Text = st.Compiler;
        txtFlags.Text = st.CxxFlags;
        lblFlagsHint.Text = "Để trống = mặc định: " + Settings.DefaultFlags;
        txtPython.Text = st.PythonCommand;
        txtTimeout.Text = st.TimeoutMs.ToString();
        txtCompileTimeout.Text = st.CompileTimeoutSec.ToString();
        txtMaxOutput.Text = Util.LL(st.MaxOutputMB);
        txtMemory.Text = Util.LL(st.MemoryMB);
        txtOutputRoot.Text = st.OutputRoot;
        txtPrefix.Text = st.Prefix;
        chkNormalizeEol.IsChecked = st.NormalizeEol;
        chkZipCompress.IsChecked = st.ZipCompress;
        lblPath.Text = "File cấu hình: " + (st.SourcePath ?? Settings.FileName);
    }

    private void BtnBrowseRoot_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục output gốc" };
        if (dlg.ShowDialog(this) == true) txtOutputRoot.Text = dlg.FolderName;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private bool TryNum(string text, long lo, long hi, string name, out long value)
    {
        if (Util.ParseLL(text, out value) && value >= lo && value <= hi) return true;
        ShowError($"{name} phải là số nguyên trong [{Util.LL(lo)}, {Util.LL(hi)}].");
        return false;
    }

    private void ShowError(string msg)
    {
        lblError.Text = "⚠ " + msg;
        lblError.Visibility = Visibility.Visible;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        lblError.Visibility = Visibility.Collapsed;
        if (!TryNum(txtTimeout.Text, 1, 3600000, "Thời gian tối đa mỗi test", out long timeout)) return;
        if (!TryNum(txtCompileTimeout.Text, 1, 3600, "Thời gian biên dịch tối đa", out long compileTimeout)) return;
        if (!TryNum(txtMaxOutput.Text, 1, 4096, "Output tối đa mỗi test", out long maxOutput)) return;
        if (!TryNum(txtMemory.Text, 0, 1048576, "Giới hạn bộ nhớ", out long memory)) return;
        string prefix = txtPrefix.Text.Trim();
        if (prefix.Length == 0 || !prefix.All(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-'))
        {
            ShowError("Tiền tố tên file chỉ được chứa chữ, số, '_' và '-' (không được rỗng).");
            return;
        }
        string root = txtOutputRoot.Text.Trim();
        if (root.Length == 0) { ShowError("Thư mục output gốc không được rỗng."); return; }
        string compiler = txtCompiler.Text.Trim();
        if (compiler.Length == 0) { ShowError("Trình biên dịch không được rỗng."); return; }

        _st.Compiler = compiler;
        _st.CxxFlags = txtFlags.Text.Trim();
        _st.PythonCommand = txtPython.Text.Trim();
        _st.TimeoutMs = (int)timeout;
        _st.CompileTimeoutSec = (int)compileTimeout;
        _st.MaxOutputMB = maxOutput;
        _st.MemoryMB = memory;
        _st.OutputRoot = root;
        _st.Prefix = prefix;
        _st.NormalizeEol = chkNormalizeEol.IsChecked == true;
        _st.ZipCompress = chkZipCompress.IsChecked == true;

        if (!_st.Save(out string? err))
        {
            MessageBox.Show(this, err ?? "Không lưu được cấu hình.", "Cài đặt", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        DialogResult = true;
    }
}
