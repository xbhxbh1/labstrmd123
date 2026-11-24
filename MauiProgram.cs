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
            .UseMauiMaps()
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
        builder.Services.AddSingleton<IInspectionRepository, InspectionRepository>();
        builder.Services.AddSingleton<ISyncService, SyncService>();
        builder.Services.AddSingleton<ILogFileReader, LogFileReader>();
        builder.Services.AddSingleton<ICrashReporter, SentryCrashReporter>();
        builder.Services.AddSingleton<IMediaPicker>(MediaPicker.Default);
        builder.Services.AddSingleton<IGeolocation>(Geolocation.Default);
        builder.Services.AddSingleton<IAccelerometer>(Accelerometer.Default);
        builder.Services.AddSingleton<IConnectivity>(Connectivity.Current);
        builder.Services.AddSingleton<IMap>(Microsoft.Maui.ApplicationModel.Map.Default);
        builder.Services.AddSingleton<IDeviceInfo>(DeviceInfo.Current);
        builder.Services.AddSingleton<IPermissionService, PermissionService>();
        builder.Services.AddSingleton<IPhotoService, PhotoService>();
        builder.Services.AddSingleton<IDeviceLocationService, DeviceLocationService>();
        builder.Services.AddSingleton<IShakeDetectionService, ShakeDetectionService>();
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
