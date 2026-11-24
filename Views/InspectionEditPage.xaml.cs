using StudentCabinetMVVM.ViewModels;

namespace StudentCabinetMVVM.Views;

public partial class InspectionEditPage : ContentPage
{
    public InspectionEditPage(InspectionEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
