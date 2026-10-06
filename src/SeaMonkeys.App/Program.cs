using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace SeaMonkeys.App;

internal static class Program
{
    private static Mutex? instanceMutex;

    [STAThread]
    private static int Main(string[] args)
    {
        instanceMutex = new Mutex(initiallyOwned: true, "SeaMonkeys.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            return 0;
        }

        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(_ =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
        finally
        {
            instanceMutex.ReleaseMutex();
            instanceMutex.Dispose();
        }

        return 0;
    }
}
