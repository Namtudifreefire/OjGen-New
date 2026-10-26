// Program.cs - Điểm vào của ojgen (dòng lệnh).
using System.Text;
using OjGen.Cli;

Console.OutputEncoding = new UTF8Encoding(false);
Console.InputEncoding = new UTF8Encoding(false);
try
{
    return CliApp.Run(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine("LỖI không mong đợi: " + ex.Message);
    return 3;
}
