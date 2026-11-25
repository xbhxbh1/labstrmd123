using FuelErp.Services;
using FuelErp.ViewModels;

namespace FuelErp.Views;

public partial class RefuelingDetailPage : ContentPage, IQueryAttributable
{
    public RefuelingDetailPage(RefuelingDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is IQueryReceiver receiver)
            receiver.ApplyQuery(query);
    }
}
