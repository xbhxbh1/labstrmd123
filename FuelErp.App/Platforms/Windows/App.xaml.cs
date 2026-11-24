using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Serilog;
using FuelErp.Services;

namespace FuelErp.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        this.InitializeComponent();

        // Платформо-специфічний перехоплювач необроблених винятків WinUI (аналог AndroidEnvironment.UnhandledExceptionRaiser).
        this.UnhandledException += (s, e) =>
        {
            Log.Fatal(e.Exception, "Необроблений виняток WinUI: {Message}", e.Message);
            Log.CloseAndFlush();
        };
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        if (activation.Kind == ExtendedActivationKind.Protocol
            && activation.Data is global::Windows.ApplicationModel.Activation.IProtocolActivatedEventArgs protocol)
        {
            var state = IPlatformApplication.Current?.Services.GetService<IAppStateService>();
            if (state is not null)
                state.PendingDeepLink = protocol.Uri.AbsoluteUri;
        }
    }
}
