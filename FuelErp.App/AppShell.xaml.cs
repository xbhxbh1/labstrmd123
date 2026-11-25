using FuelErp.Views;

namespace FuelErp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Маршрути модуля: список → картка → форма редагування
        Routing.RegisterRoute("refueling/detail", typeof(RefuelingDetailPage));
        Routing.RegisterRoute("refueling/edit", typeof(RefuelingEditPage));
        Routing.RegisterRoute("tanks", typeof(TanksPage));
        Routing.RegisterRoute("vehicles", typeof(VehiclesPage));
        Routing.RegisterRoute("report", typeof(ReportPage));
        Routing.RegisterRoute("users", typeof(UsersPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
        Routing.RegisterRoute("logs", typeof(LogsPage));
    }
}
