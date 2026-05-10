using PetDude.Services;

namespace PetDude;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        try
        {
            LogService.Write("Program.Main entered");
            var app = new App();

            var window = new MainWindow();
            app.MainWindow = window;

            window.Show();
            LogService.Write("MainWindow.Show from Program.Main");

            app.Run();
        }
        catch (Exception exception)
        {
            LogService.Write(exception);
            throw;
        }
    }
}
