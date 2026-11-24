using Sentry.Maui;

namespace StudentCabinetMVVM.Services;

public static class CrashReportingConfig
{
    public const string DsnKey = "sentry_dsn";

    public static string GetDsn() => Preferences.Get(DsnKey, string.Empty);

    public static void Configure(SentryMauiOptions options)
    {
        // Порожній DSN вимикає SDK: застосунок працює без хмарного сервісу.
        options.Dsn = GetDsn();
        options.AutoSessionTracking = true;
        options.AttachStacktrace = true;
        options.SendDefaultPii = false;
#if DEBUG
        options.Environment = "debug";
#else
        options.Environment = "release";
#endif
    }
}
