namespace FuelErp.Services;

public static class AppPaths
{
    public static string LogsDirectory => Path.Combine(FileSystem.AppDataDirectory, "logs");
}
