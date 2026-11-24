using Microsoft.Extensions.Logging;
using Serilog;
using FuelErp.Data;
using FuelErp.Services;
using FuelErp.ViewModels;
using FuelErp.Views;

namespace FuelErp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        Log.Logger = LoggingConfiguration.CreateLogger();
        LoggingConfiguration.RegisterGlobalHandlers();
        Log.Information("Запуск застосунку FuelErp");

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSentry(CrashReportingConfig.Configure)
            .UseMauiMaps()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Logging.AddSerilog(Log.Logger, dispose: true);

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "students.db3");

        // Інтерфейси MAUI Essentials (лабораторна робота 7)
        builder.Services.AddSingleton<IMediaPicker>(MediaPicker.Default);
        builder.Services.AddSingleton<IGeolocation>(Geolocation.Default);
        builder.Services.AddSingleton<IAccelerometer>(Accelerometer.Default);
        builder.Services.AddSingleton<IConnectivity>(Connectivity.Current);
        builder.Services.AddSingleton<IMap>(Microsoft.Maui.ApplicationModel.Map.Default);
        builder.Services.AddSingleton<IDeviceInfo>(DeviceInfo.Current);

        // Реалізації абстракцій ядра поверх платформних API (лабораторна робота 8)
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IDialogService, MauiDialogService>();
        builder.Services.AddSingleton<ISettingsService, MauiSettingsService>();
        builder.Services.AddSingleton<ISecureStoreService, MauiSecureStoreService>();
        builder.Services.AddSingleton<IMainThreadService, MauiMainThreadService>();
        builder.Services.AddSingleton<IThemeService, MauiThemeService>();
        builder.Services.AddSingleton<IMapLauncher, MauiMapLauncher>();
        builder.Services.AddSingleton<INetworkStatusService, MauiNetworkStatusService>();
        builder.Services.AddSingleton<IPermissionService, PermissionService>();
        builder.Services.AddSingleton<IPhotoService, PhotoService>();
        builder.Services.AddSingleton<IDeviceLocationService, DeviceLocationService>();
        builder.Services.AddSingleton<IShakeDetectionService, ShakeDetectionService>();
        builder.Services.AddSingleton<ICrashReporter, SentryCrashReporter>();
        builder.Services.AddSingleton<ISyncService, SyncService>();
        builder.Services.AddSingleton<ILogFileReader>(_ => new LogFileReader(AppPaths.LogsDirectory));
#if WINDOWS
        builder.Services.AddSingleton<IBiometricService, FuelErp.WinUI.WindowsBiometricService>();
#else
        builder.Services.AddSingleton<IBiometricService, NoBiometricService>();
#endif

        // Логіка ядра
        builder.Services.AddSingleton<IAppStateService, AppStateService>();
        builder.Services.AddSingleton(new AppDatabase(dbPath));
        builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();
        builder.Services.AddSingleton<IStudentRepository, StudentRepository>();
        builder.Services.AddSingleton<IInspectionRepository, InspectionRepository>();
        builder.Services.AddSingleton<ISessionService, SessionService>();
        builder.Services.AddSingleton<IAuthApi, HttpAuthApi>();
        builder.Services.AddSingleton<IAuthService, AuthService>();

        // ViewModel (ядро) і сторінки (View)
        builder.Services.AddTransient<StudentViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<StudentDetailViewModel>();
        builder.Services.AddTransient<StudentDetailPage>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<UsersViewModel>();
        builder.Services.AddTransient<UsersPage>();
        builder.Services.AddTransient<InspectionsViewModel>();
        builder.Services.AddTransient<InspectionsPage>();
        builder.Services.AddTransient<InspectionEditViewModel>();
        builder.Services.AddTransient<InspectionEditPage>();
        builder.Services.AddTransient<InspectionDetailViewModel>();
        builder.Services.AddTransient<InspectionDetailPage>();
        builder.Services.AddTransient<InspectionsMapViewModel>();
        builder.Services.AddTransient<InspectionsMapPage>();
        builder.Services.AddTransient<LogsViewModel>();
        builder.Services.AddTransient<LogsPage>();

        return builder.Build();
    }
}
