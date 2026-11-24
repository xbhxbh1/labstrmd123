namespace FuelErp.Services;

public class MauiDialogService : IDialogService
{
    public Task AlertAsync(string title, string message, string accept = "OK") =>
        Shell.Current.DisplayAlert(title, message, accept);

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        Shell.Current.DisplayAlert(title, message, accept, cancel);
}
