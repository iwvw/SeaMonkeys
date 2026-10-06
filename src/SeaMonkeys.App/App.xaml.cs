using Microsoft.UI.Xaml;

namespace SeaMonkeys.App;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;

    public MainWindow? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SeaMonkeys");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "last-error.log"), $"{DateTime.Now:O}\n{e.Exception}");
        }
        catch
        {
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
