using Uno.UI.Hosting;

namespace Atlas.App;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // "Atlas.App.exe path\to\model.json" or Windows "Open with" → boot straight into that model.
        App.StartupModelPath = args.FirstOrDefault(a =>
            a.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(a));

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }
}
