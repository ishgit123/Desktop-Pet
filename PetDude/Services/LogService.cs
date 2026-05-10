using System.IO;

namespace PetDude.Services;

public static class LogService
{
    private static readonly object Gate = new();
    private static readonly string DirectoryPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PetDude");

    public static string PathToLog => Path.Combine(DirectoryPath, "petdude.log");

    public static void Write(string message)
    {
        var line = $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}";
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                File.AppendAllText(PathToLog, line);
            }
        }
        catch
        {
            try
            {
                File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "petdude.log"), line);
            }
            catch
            {
            }
        }
    }

    public static void Write(Exception exception)
    {
        Write(exception.ToString());
    }
}
