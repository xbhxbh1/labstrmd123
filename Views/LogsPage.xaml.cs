using StudentCabinetMVVM.ViewModels;

namespace StudentCabinetMVVM.Views;

public partial class LogsPage : ContentPage
{
    public LogsPage(LogsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is LogsViewModel vm)
            vm.RefreshCommand.Execute(null);
    }
}
