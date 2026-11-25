using FuelErp.Services;
using FuelErp.ViewModels;

namespace FuelErp.Views;

public partial class RefuelingEditPage : ContentPage, IQueryAttributable
{
    public RefuelingEditPage(RefuelingEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is IQueryReceiver receiver)
            receiver.ApplyQuery(query);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is RefuelingEditViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
