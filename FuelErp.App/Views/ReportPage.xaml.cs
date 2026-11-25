using FuelErp.ViewModels;

namespace FuelErp.Views;

public partial class ReportPage : ContentPage
{
    public ReportPage(ReportViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is ReportViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
