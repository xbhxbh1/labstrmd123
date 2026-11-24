using Microsoft.Extensions.Logging;
using StudentCabinetMVVM.ViewModels;

namespace StudentCabinetMVVM;

public partial class MainPage : ContentPage
{
    private readonly ILogger<MainPage> _logger;

    public MainPage(StudentViewModel viewModel, ILogger<MainPage> logger)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _logger = logger;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _logger.LogInformation("Життєвий цикл сторінки: MainPage.OnAppearing");

        if (BindingContext is StudentViewModel vm)
            vm.LoadCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _logger.LogInformation("Життєвий цикл сторінки: MainPage.OnDisappearing (перехід на іншу сторінку або згортання)");
    }
}
