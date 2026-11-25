using FuelErp.ViewModels;

namespace FuelErp.Views;

public partial class TanksPage : ContentPage
{
    public TanksPage(TanksViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is TanksViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
