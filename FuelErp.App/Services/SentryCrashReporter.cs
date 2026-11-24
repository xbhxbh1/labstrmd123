using Microsoft.Extensions.Logging;
using Sentry;

namespace FuelErp.Services;

public class SentryCrashReporter : ICrashReporter
{
    private readonly ILogger<SentryCrashReporter> _logger;

    public SentryCrashReporter(ILogger<SentryCrashReporter> logger)
    {
        _logger = logger;
    }

    public bool IsEnabled => SentrySdk.IsEnabled;

    public string Dsn => CrashReportingConfig.GetDsn();

    public void SaveDsn(string dsn)
    {
        Preferences.Set(CrashReportingConfig.DsnKey, (dsn ?? string.Empty).Trim());
        _logger.LogInformation("DSN сервісу збору крашів збережено (набуде чинності після перезапуску)");
    }

    public string? SendTestReport()
    {
        if (!SentrySdk.IsEnabled)
        {
            _logger.LogWarning("Сервіс збору крашів вимкнено (DSN не задано), тестовий звіт не надіслано");
            return null;
        }

        var id = SentrySdk.CaptureException(
            new InvalidOperationException("Тестовий звіт про помилку (FuelErp)"));
        _logger.LogInformation("Тестовий звіт надіслано в Sentry, EventId={EventId}", id);
        return id.ToString();
    }

    public async Task FlushAsync()
    {
        if (SentrySdk.IsEnabled)
            await SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));
    }
}
