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
        Log.Information("Запуск застосунку FuelErp (облік палива та мастильних матеріалів)");

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

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "fuelerp.db3");

        // Інтерфейси MAUI Essentials
        builder.Services.AddSingleton<IMediaPicker>(MediaPicker.Default);
        builder.Services.AddSingleton<IGeolocation>(Geolocation.Default);
        builder.Services.AddSingleton<IConnectivity>(Connectivity.Current);
        builder.Services.AddSingleton<IMap>(Microsoft.Maui.ApplicationModel.Map.Default);
        builder.Services.AddSingleton<IDeviceInfo>(DeviceInfo.Current);

        // Реалізації абстракцій ядра поверх платформних API (Singleton: не мають стану сторінки, одна копія на застосунок)
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
        builder.Services.AddSingleton<ICrashReporter, SentryCrashReporter>();
        builder.Services.AddSingleton<ISyncService, SyncService>();
        builder.Services.AddSingleton<ILogFileReader>(_ => new LogFileReader(AppPaths.LogsDirectory));
#if WINDOWS
        builder.Services.AddSingleton<IBiometricService, FuelErp.WinUI.WindowsBiometricService>();
#else
        builder.Services.AddSingleton<IBiometricService, NoBiometricService>();
#endif

        // Логіка ядра. Singleton: стан сесії, одне підключення до БД, сервіси без стану
        builder.Services.AddSingleton<IAppStateService, AppStateService>();
        builder.Services.AddSingleton(new AppDatabase(dbPath));
        builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();
        builder.Services.AddSingleton<ISessionService, SessionService>();
        builder.Services.AddSingleton<IAuthApi, HttpAuthApi>();
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IVehicleRepository, VehicleRepository>();
        builder.Services.AddSingleton<ITankRepository, TankRepository>();
        builder.Services.AddSingleton<IRefuelingRepository, RefuelingRepository>();
        builder.Services.AddSingleton<IFuelDataSeeder, FuelDataSeeder>();

        // ViewModel і сторінки (Transient: кожен перехід створює нову сторінку з власним станом форми)
        builder.Services.AddTransient<RefuelingsViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<RefuelingDetailViewModel>();
        builder.Services.AddTransient<RefuelingDetailPage>();
        builder.Services.AddTransient<RefuelingEditViewModel>();
        builder.Services.AddTransient<RefuelingEditPage>();
        builder.Services.AddTransient<TanksViewModel>();
        builder.Services.AddTransient<TanksPage>();
        builder.Services.AddTransient<VehiclesViewModel>();
        builder.Services.AddTransient<VehiclesPage>();
        builder.Services.AddTransient<ReportViewModel>();
        builder.Services.AddTransient<ReportPage>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<UsersViewModel>();
        builder.Services.AddTransient<UsersPage>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<LogsViewModel>();
        builder.Services.AddTransient<LogsPage>();

        return builder.Build();
    }
}
