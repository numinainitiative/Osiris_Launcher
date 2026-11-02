using System.Threading;
internal static class Program
{
    static void Main(string[] args)
    {
        if(args.Length==1)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(args[0]){UseShellExecute=false,CreateNoWindow=true});
            Thread.Sleep(100);return;
        }
        Thread.Sleep(2000);
    }
}
