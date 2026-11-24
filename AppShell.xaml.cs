using StudentCabinetMVVM.Views;

namespace StudentCabinetMVVM;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("studentdetail", typeof(StudentDetailPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
        Routing.RegisterRoute("logs", typeof(LogsPage));
        Routing.RegisterRoute("users", typeof(UsersPage));
        Routing.RegisterRoute("inspections", typeof(InspectionsPage));
        Routing.RegisterRoute("inspection/new", typeof(InspectionEditPage));
        Routing.RegisterRoute("inspection/detail", typeof(InspectionDetailPage));
        Routing.RegisterRoute("inspections/map", typeof(InspectionsMapPage));
    }
}
