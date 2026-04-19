using System.Threading;

namespace SingTray.Client;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, @"Global\SingTray.Client.Singleton", out var createdNew);
        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetCompatibleTextRenderingDefault(false);

        using var pipeClient = new PipeClient();
        using var importService = new FileImportService();
        using var desiredStateStore = new DesiredStateStore();
        using var clientLogService = new ClientLogService();
        using var context = new TrayApplicationContext(pipeClient, importService, desiredStateStore, clientLogService);
        Application.Run(context);
    }
}
