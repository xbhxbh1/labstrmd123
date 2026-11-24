using FuelErp.ViewModels;

namespace FuelErp.Views;

public partial class InspectionsMapPage : ContentPage
{
    public InspectionsMapPage(InspectionsMapViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is InspectionsMapViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
