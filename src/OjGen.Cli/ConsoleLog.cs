// ConsoleLog.cs - Ghi nhật ký của phần lõi ra console.
using OjGen.Core;

namespace OjGen.Cli;

internal sealed class ConsoleLog : IJobLog
{
    private readonly bool _live = !Console.IsOutputRedirected;

    public void Write(string text) => Console.Out.Write(text);

    public void Progress(string phase, int done, int total)
    {
        // Chỉ hiển thị tiến độ sinh input ở console thật (giống bản C++); các giai đoạn khác đã có dòng riêng.
        if (!_live || phase != "Sinh input") return;
        if (done == total || done % 10 == 0)
        {
            Console.Out.Write($"\r  Đã sinh {done}/{total} file input");
            if (done == total) Console.Out.Write("\n");
        }
    }
}
