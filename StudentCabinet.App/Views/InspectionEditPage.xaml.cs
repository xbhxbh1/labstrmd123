using StudentCabinetMVVM.Services;
using StudentCabinetMVVM.ViewModels;

namespace StudentCabinetMVVM.Views;

public partial class InspectionEditPage : ContentPage, IQueryAttributable
{
    public InspectionEditPage(InspectionEditViewModel viewModel)
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
