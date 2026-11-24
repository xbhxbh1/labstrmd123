namespace FuelErp.Services;

public interface ICrashReporter
{
    bool IsEnabled { get; }
    string Dsn { get; }
    void SaveDsn(string dsn);
    string? SendTestReport();
    Task FlushAsync();
}
