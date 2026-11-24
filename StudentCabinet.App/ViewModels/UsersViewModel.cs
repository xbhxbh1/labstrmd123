using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StudentCabinetMVVM.Models;
using StudentCabinetMVVM.Services;

namespace StudentCabinetMVVM.ViewModels;

/// <summary>
/// Розділ «Керування користувачами» (лише для адміністратора). Список беремо із сервера
/// з токеном доступу: сервер сам перевіряє роль, тому навіть обхід UI не дасть доступу.
/// </summary>
public partial class UsersViewModel : ObservableObject
{
    private readonly IAuthApi _api;
    private readonly IAuthService _auth;
    private readonly ILogger<UsersViewModel> _logger;

    public ObservableCollection<UserInfo> Users { get; } = new();

    [ObservableProperty]
    private string _status = string.Empty;

    public UsersViewModel(IAuthApi api, IAuthService auth, ILogger<UsersViewModel> logger)
    {
        _api = api;
        _auth = auth;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        Users.Clear();

        var token = await _auth.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Status = "Токен доступу відсутній. Увійдіть знову.";
            return;
        }

        try
        {
            var users = await _api.GetUsersAsync(token);
            foreach (var user in users)
                Users.Add(user);

            Status = $"Користувачів: {Users.Count}";
            _logger.LogInformation("Отримано список користувачів із сервера: {Count}", Users.Count);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("Сервер відмовив у доступі до списку користувачів (403)");
            Status = "Сервер відмовив у доступі (403): потрібна роль «Адміністратор».";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("Токен не прийнято сервером (401)");
            Status = "Сесія завершилася (401). Увійдіть знову.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося отримати список користувачів");
            Status = "Не вдалося отримати список користувачів із сервера.";
        }
    }

    [RelayCommand]
    private Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
