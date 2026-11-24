using Microsoft.Extensions.Logging;
using FuelErp.Services;
using FuelErp.ViewModels;

namespace FuelErp;

public partial class MainPage : ContentPage, IQueryAttributable
{
    private readonly ILogger<MainPage> _logger;

    public MainPage(StudentViewModel viewModel, ILogger<MainPage> logger)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _logger = logger;
    }

    // Shell передає параметри навігації сторінці, а вона пересилає їх у ViewModel ядра
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is IQueryReceiver receiver)
            receiver.ApplyQuery(query);
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
