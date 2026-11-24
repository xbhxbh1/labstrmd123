using StudentCabinetMVVM.ViewModels;

namespace StudentCabinetMVVM.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
