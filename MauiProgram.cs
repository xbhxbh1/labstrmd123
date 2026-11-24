using Microsoft.Extensions.Logging;
using Serilog;
using StudentCabinetMVVM.Data;
using StudentCabinetMVVM.Services;
using StudentCabinetMVVM.ViewModels;
using StudentCabinetMVVM.Views;

namespace StudentCabinetMVVM;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        Log.Logger = LoggingConfiguration.CreateLogger();
        LoggingConfiguration.RegisterGlobalHandlers();
        Log.Information("Запуск застосунку StudentCabinetMVVM");

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSentry(CrashReportingConfig.Configure)
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Logging.AddSerilog(Log.Logger, dispose: true);

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "students.db3");

        builder.Services.AddSingleton<IAppStateService, AppStateService>();
        builder.Services.AddSingleton(new AppDatabase(dbPath));
        builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();
        builder.Services.AddSingleton<IStudentRepository, StudentRepository>();
        builder.Services.AddSingleton<ISyncService, SyncService>();
        builder.Services.AddSingleton<ILogFileReader, LogFileReader>();
        builder.Services.AddSingleton<ICrashReporter, SentryCrashReporter>();
        builder.Services.AddSingleton<ISessionService, SessionService>();
        builder.Services.AddSingleton<IAuthApi, HttpAuthApi>();
        builder.Services.AddSingleton<IAuthService, AuthService>();
#if WINDOWS
        builder.Services.AddSingleton<IBiometricService, StudentCabinetMVVM.WinUI.WindowsBiometricService>();
#else
        builder.Services.AddSingleton<IBiometricService, NoBiometricService>();
#endif

        builder.Services.AddTransient<StudentViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<UsersViewModel>();
        builder.Services.AddTransient<UsersPage>();
        builder.Services.AddTransient<LogsViewModel>();
        builder.Services.AddTransient<LogsPage>();

        return builder.Build();
    }
}
