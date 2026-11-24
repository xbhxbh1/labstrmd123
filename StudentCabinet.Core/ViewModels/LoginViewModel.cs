using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StudentCabinetMVVM.Services;

namespace StudentCabinetMVVM.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly IBiometricService _biometrics;
    private readonly INavigationService _navigation;
    private readonly ILogger<LoginViewModel> _logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _login = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    [NotifyCanExecuteChangedFor(nameof(BiometricLoginCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _canUseBiometrics;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBiometricHint))]
    private string _biometricHint = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasBiometricHint => !string.IsNullOrEmpty(BiometricHint);

    public LoginViewModel(IAuthService auth, IBiometricService biometrics, INavigationService navigation,
        ILogger<LoginViewModel> logger)
    {
        _auth = auth;
        _biometrics = biometrics;
        _navigation = navigation;
        _logger = logger;
    }

    private bool CanLogin() =>
        !IsBusy && !string.IsNullOrWhiteSpace(Login) && !string.IsNullOrEmpty(Password);

    /// <summary>
    /// Біометричний вхід пропонується лише тоді, коли токен уже збережено
    /// (користувач раніше входив із паролем) і пристрій підтримує перевірку.
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        CanUseBiometrics = false;
        BiometricHint = string.Empty;

        if (!await _auth.HasStoredTokenAsync())
            return;

        if (await _biometrics.IsAvailableAsync())
            CanUseBiometrics = true;
        else
            BiometricHint = "Біометричний вхід недоступний: на пристрої не налаштовано Windows Hello (PIN-код, відбиток пальця або обличчя).";
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _auth.LoginAsync(Login.Trim(), Password);
            if (!result.Success)
            {
                ErrorMessage = result.Error ?? "Не вдалося увійти.";
                return;
            }

            Password = string.Empty;
            await _navigation.GoToAsync("//MainPage");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseBiometricLogin))]
    private async Task BiometricLoginAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            var verified = await _biometrics.AuthenticateAsync("Підтвердьте особу для входу до кабінету студента");
            if (!verified)
            {
                ErrorMessage = "Біометричну перевірку не пройдено.";
                return;
            }

            var result = await _auth.LoginWithStoredTokenAsync();
            if (!result.Success)
            {
                ErrorMessage = result.Error ?? "Не вдалося увійти.";
                CanUseBiometrics = await _auth.HasStoredTokenAsync();
                return;
            }

            await _navigation.GoToAsync("//MainPage");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanUseBiometricLogin() => !IsBusy;
}
