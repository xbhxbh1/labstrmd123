using StudentCabinetMVVM.ViewModels;

namespace StudentCabinetMVVM.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
