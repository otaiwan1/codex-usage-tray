using System.Threading;

namespace CodexUsageTray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        const string mutexName = "Local\\CodexUsageTray.SingleInstance";
        using var mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
