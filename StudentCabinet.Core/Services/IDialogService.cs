namespace StudentCabinetMVVM.Services;

/// <summary>Діалоги з користувачем (замість Shell.Current.DisplayAlert у ViewModel).</summary>
public interface IDialogService
{
    Task AlertAsync(string title, string message, string accept = "OK");

    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
}
