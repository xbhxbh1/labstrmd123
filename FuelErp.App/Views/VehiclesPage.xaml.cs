using FuelErp.ViewModels;

namespace FuelErp.Views;

public partial class VehiclesPage : ContentPage
{
    public VehiclesPage(VehiclesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is VehiclesViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
