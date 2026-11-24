using FuelErp.ViewModels;

namespace FuelErp.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // після виходу токен видаляється, тому доступність біометричного входу перевіряється щоразу
        if (BindingContext is LoginViewModel vm)
            vm.InitializeCommand.Execute(null);
    }
}
