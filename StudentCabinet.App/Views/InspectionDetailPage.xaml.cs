using StudentCabinetMVVM.ViewModels;

namespace StudentCabinetMVVM.Views;

public partial class InspectionDetailPage : ContentPage
{
    public InspectionDetailPage(InspectionDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
