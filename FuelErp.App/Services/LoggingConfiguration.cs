using Serilog;
using Serilog.Events;

namespace FuelErp.Services;

public static class LoggingConfiguration
{
    public const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}";

    public static Serilog.ILogger CreateLogger()
    {
        Directory.CreateDirectory(AppPaths.LogsDirectory);

        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Debug(outputTemplate: OutputTemplate)
            .WriteTo.Console(outputTemplate: OutputTemplate)
            .WriteTo.File(
                Path.Combine(AppPaths.LogsDirectory, "app-.log"),
                outputTemplate: OutputTemplate,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true)
            .CreateLogger();
    }

    public static void RegisterGlobalHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Log.Fatal(ex, "Необроблений виняток у AppDomain (IsTerminating={IsTerminating})", e.IsTerminating);

            if (e.IsTerminating)
                Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Необроблений виняток у Task");
            e.SetObserved();
        };
    }
}
