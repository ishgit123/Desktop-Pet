namespace PetDude;

public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            Services.LogService.Write(e.Exception);
            e.Handled = false;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                Services.LogService.Write(exception);
            }
            else
            {
                Services.LogService.Write($"Unhandled exception object: {e.ExceptionObject}");
            }
        };
    }

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        Services.LogService.Write("App startup");
    }
}
