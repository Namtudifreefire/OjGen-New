// ProcessRunner.cs - Chạy một tiến trình con với giới hạn thời gian/dung lượng/bộ nhớ.
//
// Tiến trình được tạo bằng danh sách đối số (ProcessStartInfo.ArgumentList), KHÔNG qua shell,
// nên đường dẫn có dấu cách/ký tự đặc biệt được truyền nguyên vẹn.
// stdin <- file input, stdout/stderr -> file (bơm dữ liệu bằng luồng nền).
//
// LƯU Ý AN TOÀN: lời giải là code do người dùng cung cấp và chạy trực tiếp trên máy với quyền
// của người dùng. ojgen chỉ giới hạn thời gian, dung lượng output và (tùy chọn) bộ nhớ;
// đây KHÔNG phải sandbox. Chỉ chạy code bạn tin tưởng.
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OjGen.Core;

public enum RunStatus { Ok, RuntimeError, Timeout, OutputLimit, StartFailed, Cancelled }

public sealed class RunLimits
{
    public int TimeoutMs { get; set; } = 2000;
    public ulong MaxOutputBytes { get; set; } = 64UL << 20;
    /// <summary>0 = không giới hạn.</summary>
    public ulong MemoryBytes { get; set; }
}

public sealed class RunResult
{
    public RunStatus Status { get; set; } = RunStatus.StartFailed;
    public long ExitCode { get; set; }
    public long ElapsedMs { get; set; }
    public string Message { get; set; } = "";
}

public static class ProcessRunner
{
    private const uint SEM_FAILCRITICALERRORS = 0x0001;
    private const uint SEM_NOGPFAULTERRORBOX = 0x0002;

    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint mode);

    static ProcessRunner()
    {
        // Tắt hộp thoại "chương trình ngừng hoạt động" của Windows; tiến trình con kế thừa thiết lập này.
        if (OperatingSystem.IsWindows())
        {
            try { SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX); } catch (Exception) { }
        }
    }

    public static string StatusName(RunStatus s) => s switch
    {
        RunStatus.Ok => "OK",
        RunStatus.RuntimeError => "Lỗi khi chạy (RE)",
        RunStatus.Timeout => "Quá thời gian (TLE)",
        RunStatus.OutputLimit => "Output quá lớn (OLE)",
        RunStatus.StartFailed => "Không khởi chạy được",
        RunStatus.Cancelled => "Đã hủy",
        _ => "?",
    };

    private static string NtStatusText(uint code) => code switch
    {
        0xC0000005 => "truy cập bộ nhớ không hợp lệ (ACCESS_VIOLATION) - thường do vượt chỉ số mảng",
        0xC00000FD => "tràn stack (STACK_OVERFLOW) - đệ quy quá sâu",
        0xC0000094 => "chia cho 0 (INTEGER_DIVIDE_BY_ZERO)",
        0xC0000095 => "tràn số nguyên (INTEGER_OVERFLOW)",
        0xC000008E => "chia số thực cho 0",
        0xC0000017 => "không đủ bộ nhớ (NO_MEMORY)",
        0xC0000409 => "lỗi kiểm tra bộ đệm / abort (STACK_BUFFER_OVERRUN)",
        0xC0000135 => "thiếu DLL (DLL_NOT_FOUND) - hãy biên dịch với -static",
        0xC000013A => "bị ngắt bằng Ctrl+C",
        0xC0000139 => "không tìm thấy hàm trong DLL (ENTRYPOINT_NOT_FOUND)",
        3 => "thường do abort()/assert thất bại (hoặc chương trình tự trả về 3)",
        _ => "",
    };

    private static string SignalText(int sig) => sig switch
    {
        11 => "SIGSEGV - truy cập bộ nhớ không hợp lệ (vượt chỉ số mảng, tràn stack)",
        8 => "SIGFPE - lỗi số học (chia cho 0)",
        6 => "SIGABRT - chương trình gọi abort() (assert thất bại, bad_alloc...)",
        7 => "SIGBUS - lỗi bus",
        9 => "SIGKILL - bị hệ thống dừng (có thể hết bộ nhớ)",
        25 => "SIGXFSZ - output vượt giới hạn dung lượng",
        _ => "tín hiệu " + sig,
    };

    /// <summary>
    /// Chạy argv với stdin = inFile (null/rỗng = không có dữ liệu), stdout -> outFile, stderr -> errFile
    /// (errFile = outFile thì gộp chung). unixLimits: trên Linux/macOS chạy qua /bin/sh để đặt ulimit
    /// (stack 256 MB, và giới hạn bộ nhớ ảo nếu có).
    /// </summary>
    public static RunResult Run(IReadOnlyList<string> argv, string? inFile, string outFile, string errFile,
                                string workDir, RunLimits lim, CancellationToken ct = default, bool unixLimits = false)
    {
        var res = new RunResult();
        FileStream? fin = null, fout = null, ferr = null;
        Process? p = null;
        try
        {
            if (!string.IsNullOrEmpty(inFile))
            {
                try { fin = new FileStream(inFile, FileMode.Open, FileAccess.Read, FileShare.Read); }
                catch (Exception) { res.Message = "Không mở được file input: " + inFile; return res; }
            }
            try { fout = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.Read); }
            catch (Exception) { res.Message = "Không tạo được file output tạm: " + outFile; return res; }
            bool sameErr = string.Equals(Path.GetFullPath(errFile), Path.GetFullPath(outFile), StringComparison.Ordinal);
            if (!sameErr)
            {
                try { ferr = new FileStream(errFile, FileMode.Create, FileAccess.Write, FileShare.Read); }
                catch (Exception) { res.Message = "Không tạo được file stderr tạm."; return res; }
            }

            var psi = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            if (!string.IsNullOrEmpty(workDir)) psi.WorkingDirectory = workDir;
            bool wrapped = unixLimits && !OperatingSystem.IsWindows() && File.Exists("/bin/sh");
            if (wrapped)
            {
                string script = "ulimit -s 262144 2>/dev/null || ulimit -s unlimited 2>/dev/null || ulimit -s \"$(ulimit -H -s)\" 2>/dev/null; ";
                if (lim.MemoryBytes > 0)
                    script += "ulimit -v " + Util.LL(lim.MemoryBytes / 1024 + 1) + " 2>/dev/null; ";
                script += "exec \"$@\"";
                psi.FileName = "/bin/sh";
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add(script);
                psi.ArgumentList.Add("sh");
                foreach (var a in argv) psi.ArgumentList.Add(a);
            }
            else
            {
                psi.FileName = argv[0];
                for (int i = 1; i < argv.Count; i++) psi.ArgumentList.Add(argv[i]);
            }

            try { p = Process.Start(psi); }
            catch (Win32Exception ex)
            {
                res.Status = RunStatus.StartFailed;
                if (ex.NativeErrorCode == 2 || ex.NativeErrorCode == 3)
                    res.Message = OperatingSystem.IsWindows()
                        ? $"Không tìm thấy chương trình '{argv[0]}'. Kiểm tra PATH (vd C:\\msys64\\ucrt64\\bin)."
                        : $"Không tìm thấy chương trình '{argv[0]}' (kiểm tra PATH).";
                else
                    res.Message = $"Không khởi chạy được '{argv[0]}': {ex.Message}";
                return res;
            }
            catch (Exception ex)
            {
                res.Status = RunStatus.StartFailed;
                res.Message = $"Không khởi chạy được '{argv[0]}': {ex.Message}";
                return res;
            }
            if (p == null) { res.Status = RunStatus.StartFailed; res.Message = "Không khởi chạy được tiến trình."; return res; }

            var sw = Stopwatch.StartNew();
            object sink = new();
            ulong outTotal = 0;
            bool exceeded = false;

            // stdin <- file
            var pumpIn = Task.Run(() =>
            {
                try
                {
                    var stdin = p.StandardInput.BaseStream;
                    if (fin != null) fin.CopyTo(stdin);
                    stdin.Flush();
                }
                catch (Exception) { /* tiến trình đóng stdin sớm: bình thường */ }
                finally { try { p.StandardInput.Close(); } catch (Exception) { } }
            });

            // stdout -> file
            var pumpOut = Task.Run(() =>
            {
                try
                {
                    var s = p.StandardOutput.BaseStream;
                    var b = new byte[1 << 16];
                    int n;
                    while ((n = s.Read(b, 0, b.Length)) > 0)
                    {
                        lock (sink)
                        {
                            outTotal += (ulong)n;
                            if (outTotal > lim.MaxOutputBytes) { exceeded = true; return; }
                            fout.Write(b, 0, n);
                        }
                    }
                }
                catch (Exception) { }
            });

            // stderr -> file (gộp chung hoặc riêng, riêng thì giữ tối đa 1 MB)
            var pumpErr = Task.Run(() =>
            {
                try
                {
                    var s = p.StandardError.BaseStream;
                    var b = new byte[1 << 14];
                    int n;
                    long kept = 0;
                    while ((n = s.Read(b, 0, b.Length)) > 0)
                    {
                        lock (sink)
                        {
                            if (ferr == null)
                            {
                                outTotal += (ulong)n;
                                if (outTotal > lim.MaxOutputBytes) { exceeded = true; return; }
                                fout.Write(b, 0, n);
                            }
                            else if (kept < (1 << 20))
                            {
                                ferr.Write(b, 0, n);
                                kept += n;
                            }
                        }
                    }
                }
                catch (Exception) { }
            });

            res.Status = RunStatus.Ok;
            bool exited = false, memExceeded = false;
            long nextMemCheck = 0;
            ulong memUsed = 0;
            while (true)
            {
                if (p.WaitForExit(2)) { exited = true; break; }
                if (ct.IsCancellationRequested) { res.Status = RunStatus.Cancelled; break; }
                if (exceeded) { res.Status = RunStatus.OutputLimit; break; }
                long ms = sw.ElapsedMilliseconds;
                if (ms > lim.TimeoutMs) { res.Status = RunStatus.Timeout; break; }
                if (lim.MemoryBytes > 0 && ms >= nextMemCheck)
                {
                    nextMemCheck = ms + 10;
                    try
                    {
                        p.Refresh();
                        memUsed = (ulong)Math.Max(p.PeakWorkingSet64, 0);
                        if (memUsed > lim.MemoryBytes) { memExceeded = true; break; }
                    }
                    catch (Exception) { }
                }
            }
            if (!exited)
            {
                try { p.Kill(entireProcessTree: true); } catch (Exception) { }
                try { p.WaitForExit(5000); } catch (Exception) { }
            }
            res.ElapsedMs = sw.ElapsedMilliseconds;
            try { Task.WaitAll(new[] { pumpIn, pumpOut, pumpErr }, 2000); } catch (Exception) { }

            int code = 0;
            try { code = p.ExitCode; } catch (Exception) { }
            lock (sink)
            {
                if (res.Status == RunStatus.Ok && outTotal > lim.MaxOutputBytes) exceeded = true;
            }
            if (res.Status == RunStatus.Ok && exceeded) res.Status = RunStatus.OutputLimit;

            if (res.Status == RunStatus.Cancelled)
            {
                res.Message = "Đã hủy theo yêu cầu.";
            }
            else if (res.Status == RunStatus.Timeout)
            {
                res.Message = $"Chạy quá {lim.TimeoutMs} ms nên đã bị dừng.";
            }
            else if (res.Status == RunStatus.OutputLimit)
            {
                res.Message = $"Output vượt {Util.FmtBytes(lim.MaxOutputBytes)} nên đã bị dừng.";
            }
            else if (memExceeded)
            {
                res.Status = RunStatus.RuntimeError;
                res.ExitCode = code;
                res.Message = $"Vượt giới hạn bộ nhớ {Util.FmtBytes(lim.MemoryBytes)} (đã dùng khoảng {Util.FmtBytes(memUsed)}).";
            }
            else if (OperatingSystem.IsWindows())
            {
                uint ucode = unchecked((uint)code);
                res.ExitCode = ucode;
                if (ucode != 0)
                {
                    res.Status = RunStatus.RuntimeError;
                    string t = NtStatusText(ucode);
                    res.Message = $"Mã thoát {Util.LL(ucode)} (0x{ucode:X8})" + (t.Length == 0 ? "" : ": " + t);
                    if (lim.MemoryBytes > 0 && (ucode == 0xC0000017 || ucode == 0xC0000005 || ucode == 3))
                        res.Message += $". Có thể đã vượt giới hạn bộ nhớ {Util.FmtBytes(lim.MemoryBytes)}.";
                }
            }
            else if (code != 0)
            {
                res.ExitCode = code;
                if (code > 128 && code <= 128 + 64)
                {
                    int sig = code - 128;
                    if (sig == 25)
                    {
                        res.Status = RunStatus.OutputLimit;
                        res.Message = $"Output vượt {Util.FmtBytes(lim.MaxOutputBytes)} nên đã bị dừng.";
                    }
                    else
                    {
                        res.Status = RunStatus.RuntimeError;
                        res.Message = "Bị dừng bởi " + SignalText(sig);
                        if (lim.MemoryBytes > 0 && (sig == 11 || sig == 6 || sig == 9))
                            res.Message += $". Có thể đã vượt giới hạn bộ nhớ {Util.FmtBytes(lim.MemoryBytes)}.";
                    }
                }
                else if (wrapped && code == 127 && StartsWithExecError(ferr, fout, errFile, outFile))
                {
                    res.Status = RunStatus.StartFailed;
                    res.Message = $"Không khởi chạy được chương trình '{argv[0]}' (không tìm thấy hoặc không có quyền chạy).";
                }
                else
                {
                    res.Status = RunStatus.RuntimeError;
                    res.Message = $"Mã thoát {code} (khác 0)";
                }
            }
            return res;
        }
        finally
        {
            try { p?.Dispose(); } catch (Exception) { }
            fin?.Dispose();
            fout?.Dispose();
            ferr?.Dispose();
        }
    }

    // sh báo lỗi dạng "sh: 1: exec: <tên>: not found" / "Permission denied" khi exec thất bại.
    private static bool StartsWithExecError(FileStream? ferr, FileStream? fout, string errFile, string outFile)
    {
        try
        {
            string path = ferr != null ? errFile : outFile;
            (ferr ?? fout)?.Flush();
            using var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var b = new byte[512];
            int n = s.Read(b, 0, b.Length);
            string t = System.Text.Encoding.UTF8.GetString(b, 0, n);
            return t.Contains("exec:", StringComparison.Ordinal);
        }
        catch (Exception) { return false; }
    }
}
