using System.Diagnostics;
using System.Text;
using System.Windows.Threading;
using OjGen.Core;

namespace OjGen.Gui.Services;

/// <summary>
/// Cầu nối IJobLog -> giao diện. Luồng nền ghi vào bộ đệm; luồng giao diện lấy ra theo lô
/// (tránh làm nghẽn Dispatcher khi lời giải in hàng nghìn dòng).
/// </summary>
internal sealed class UiLog : IJobLog
{
    private readonly Dispatcher _dispatcher;
    private readonly StringBuilder _buffer = new();
    private readonly object _lock = new();
    private bool _flushPending;
    private readonly Stopwatch _progressClock = Stopwatch.StartNew();
    private long _lastProgressMs = -1000;

    public event Action<string>? TextAppended;
    public event Action<string, int, int>? ProgressChanged;

    public UiLog(Dispatcher dispatcher) => _dispatcher = dispatcher;

    public void Write(string text)
    {
        if (text.Length == 0) return;
        bool schedule;
        lock (_lock)
        {
            _buffer.Append(text);
            schedule = !_flushPending;
            _flushPending = true;
        }
        if (schedule) _dispatcher.InvokeAsync(Flush, DispatcherPriority.Background);
    }

    private void Flush()
    {
        string chunk;
        lock (_lock)
        {
            chunk = _buffer.ToString();
            _buffer.Clear();
            _flushPending = false;
        }
        if (chunk.Length > 0) TextAppended?.Invoke(chunk);
    }

    public void Progress(string phase, int done, int total)
    {
        long now = _progressClock.ElapsedMilliseconds;
        if (done < total && now - _lastProgressMs < 60) return;   // tối đa ~16 lần/giây
        _lastProgressMs = now;
        _dispatcher.InvokeAsync(() => ProgressChanged?.Invoke(phase, done, total), DispatcherPriority.Background);
    }
}
