using Microsoft.Extensions.Logging;
using StudentCabinetMVVM.Services;
using global::Windows.Security.Credentials.UI;

namespace StudentCabinetMVVM.WinUI;

/// <summary>
/// Windows Hello через UserConsentVerifier. У настільному застосунку діалог потрібно
/// прив'язувати до вікна, тому використовується UserConsentVerifierInterop із дескриптором вікна.
/// </summary>
public class WindowsBiometricService : IBiometricService
{
    private readonly ILogger<WindowsBiometricService> _logger;

    public WindowsBiometricService(ILogger<WindowsBiometricService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var availability = await UserConsentVerifier.CheckAvailabilityAsync();
            _logger.LogInformation("Windows Hello: стан доступності {Availability}", availability);
            return availability == UserConsentVerifierAvailability.Available;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося перевірити доступність Windows Hello");
            return false;
        }
    }

    public async Task<bool> AuthenticateAsync(string reason)
    {
        try
        {
            var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()
                ?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (window is null)
            {
                _logger.LogWarning("Вікно застосунку недоступне, біометричну перевірку скасовано");
                return false;
            }

            var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var result = await UserConsentVerifierInterop.RequestVerificationForWindowAsync(handle, reason);

            if (result == UserConsentVerificationResult.Verified)
            {
                _logger.LogInformation("Windows Hello: особу підтверджено");
                return true;
            }

            _logger.LogWarning("Windows Hello: перевірку не пройдено ({Result})", result);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка біометричної перевірки Windows Hello");
            return false;
        }
    }
}
