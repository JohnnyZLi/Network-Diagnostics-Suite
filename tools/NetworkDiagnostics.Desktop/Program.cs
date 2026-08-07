using System.Drawing;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Planning;
using Photino.NET;
using Photino.NET.Server;

namespace NetworkDiagnostics.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--smoke-test", StringComparer.Ordinal))
        {
            var plan = NetworkDiagnosticsRunner.DescribePlan(TestProfileId.Quick, TransferMethod.Compare);
            Console.WriteLine($"Network Diagnostics Desktop smoke test: {plan.ProfileName} / {plan.Method} / {plan.TransferCapBytes}");
            return plan.DownloadStages.Count > 0 && plan.UploadStages.Count > 0 ? 0 : 1;
        }

        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        var server = PhotinoServer.CreateStaticFileServer(
            args,
            startPort: 8210,
            portRange: 40,
            webRootFolder: "wwwroot",
            out var baseUrl);
        server.StartAsync().GetAwaiter().GetResult();

        var bridge = new PhotinoDesktopBridge();
        try
        {
            var window = new PhotinoWindow()
                .SetTitle("Network Diagnostics")
                .SetUseOsDefaultSize(false)
                .SetSize(new Size(1180, 800))
                .Center()
                .SetResizable(true);

            bridge.Attach(window);
            window.Load(baseUrl);
            window.WaitForClose();
            return 0;
        }
        finally
        {
            bridge.Dispose();
            server.StopAsync().GetAwaiter().GetResult();
            server.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
