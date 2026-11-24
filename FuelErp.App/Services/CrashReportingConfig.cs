using Sentry.Maui;

namespace FuelErp.Services;

public static class CrashReportingConfig
{
    public const string DsnKey = "sentry_dsn";

    /// <summary>
    /// DSN проєкту в Sentry, який використовується, доки користувач не змінив його на екрані «Журнал подій».
    /// DSN призначений для вбудовування в клієнтські застосунки: він дозволяє лише надсилати події.
    /// </summary>
    public const string DefaultDsn =
        "https://826974fa2cc00f98fae393a62663a1af@o4512488731443200.ingest.de.sentry.io/4512488764211280";

    // Збережене значення (навіть порожнє, що вимикає SDK) має пріоритет над DefaultDsn
    public static string GetDsn() => Preferences.Get(DsnKey, DefaultDsn);

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
