using StudentCabinetMVVM.ViewModels;

namespace StudentCabinetMVVM.Views;

public partial class InspectionsPage : ContentPage
{
    public InspectionsPage(InspectionsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    // Сенсор і підписки на події стартують, коли сторінка з'являється, і зупиняються, коли вона зникає
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is InspectionsViewModel vm)
            await vm.OnAppearingAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (BindingContext is InspectionsViewModel vm)
            vm.OnDisappearing();
    }
}
