using StudentCabinetMVVM.ViewModels;

namespace StudentCabinetMVVM.Views;

public partial class InspectionsPage : ContentPage
{
    public InspectionsPage(InspectionsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is InspectionsViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
