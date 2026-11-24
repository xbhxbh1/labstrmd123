using Android.App;
using Android.Runtime;
using Serilog;

namespace FuelErp;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
		// Платформо-специфічний перехоплювач необроблених винятків Android (розділ 1.3 завдання).
		AndroidEnvironment.UnhandledExceptionRaiser += (s, e) =>
		{
			Log.Fatal(e.Exception, "Необроблений виняток Android");
			Log.CloseAndFlush();
		};
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
