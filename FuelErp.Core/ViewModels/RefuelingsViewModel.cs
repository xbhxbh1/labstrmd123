using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using FuelErp.Data;
using FuelErp.Models;
using FuelErp.Services;

namespace FuelErp.ViewModels;

/// <summary>Головний екран: список заправок із пошуком і фільтром, дії залежать від ролі.</summary>
public partial class RefuelingsViewModel : ObservableObject, IQueryReceiver
{
    private readonly IRefuelingRepository _repository;
    private readonly IFuelDataSeeder _seeder;
    private readonly ISyncService _sync;
    private readonly ISessionService _session;
    private readonly IAuthService _auth;
    private readonly IAppStateService _state;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IMainThreadService _mainThread;
    private readonly INetworkStatusService _network;
    private readonly ILogger<RefuelingsViewModel> _logger;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private List<Refueling> _all = new();
    private bool _isLoaded;

    public ObservableCollection<Refueling> Refuelings { get; } = new();

    public IReadOnlyList<FuelFilterOption> Filters => FuelFilterOption.All;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private FuelFilterOption _selectedFilter = FuelFilterOption.All[0];

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingText))]
    private int _pendingCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NetworkText))]
    private bool _isOnline;

    public string PendingText => $"У черзі синхронізації: {PendingCount}";

    public string NetworkText => IsOnline ? "● Онлайн" : "● Офлайн: зміни збережуться в черзі";

    public string CurrentUserText => _session.CurrentRole is { } role
        ? $"Користувач: {_session.UserName} ({role.ToDisplayName()})"
        : "Користувач не увійшов";

    // Елементи інтерфейсу залежать від ролі (прив'язка IsVisible у MainPage.xaml)
    public bool CanCreate => _session.IsInRole(UserRole.StationOperator);

    public bool CanDelete => _session.IsInRole(UserRole.Accountant);

    public bool IsAccountant => _session.IsInRole(UserRole.Accountant);

    public RefuelingsViewModel(
        IRefuelingRepository repository,
        IFuelDataSeeder seeder,
        ISyncService sync,
        ISessionService session,
        IAuthService auth,
        IAppStateService state,
        INavigationService navigation,
        IDialogService dialogs,
        IMainThreadService mainThread,
        INetworkStatusService network,
        ILogger<RefuelingsViewModel> logger)
    {
        _repository = repository;
        _seeder = seeder;
        _sync = sync;
        _session = session;
        _auth = auth;
        _state = state;
        _navigation = navigation;
        _dialogs = dialogs;
        _mainThread = mainThread;
        _network = network;
        _logger = logger;

        _isOnline = _network.IsOnline;

        _session.SessionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CurrentUserText));
            OnPropertyChanged(nameof(CanCreate));
            OnPropertyChanged(nameof(CanDelete));
            OnPropertyChanged(nameof(IsAccountant));
        };

        _state.DeepLinkReceived += async (_, _) => await HandleDeepLinkAsync();
        _sync.QueueChanged += (_, _) => _mainThread.BeginInvokeOnMainThread(async () => await RefreshPendingAsync());
        _network.OnlineChanged += (_, online) => _mainThread.BeginInvokeOnMainThread(() => IsOnline = online);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedFilterChanged(FuelFilterOption value) => ApplyFilter();

    /// <summary>Пошук за номером авто, ПІБ водія, резервуаром, приміткою та фільтр за видом палива.</summary>
    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        var filtered = _all
            .Where(r => SelectedFilter.Type is null || r.FuelType == SelectedFilter.Type)
            .Where(r => query.Length == 0
                        || r.VehiclePlate.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || r.DriverName.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || r.TankName.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || r.Notes.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Refuelings.Clear();
        foreach (var item in filtered)
            Refuelings.Add(item);

        SummaryText = $"Заправок: {filtered.Count} · літрів: {filtered.Sum(r => r.Liters):N1} · сума: {filtered.Sum(r => r.Total):N2} грн";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await _loadLock.WaitAsync();
        try
        {
            await _seeder.SeedAsync();

            _logger.LogInformation("Завантаження списку заправок із бази даних");
            _all = await _repository.GetAllAsync();
            _isLoaded = true;
            ApplyFilter();
            _logger.LogInformation("Завантажено заправок: {Count}", _all.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося завантажити список заправок");
            await _dialogs.AlertAsync("Помилка", "Не вдалося завантажити дані з бази.");
            return;
        }
        finally
        {
            _loadLock.Release();
        }

        await RefreshPendingAsync();
        await HandleDeepLinkAsync();
    }

    [RelayCommand]
    private async Task NewRefuelingAsync()
    {
        // Другий рівень захисту: перевірка ролі в команді, а не лише приховування кнопки
        if (!CanCreate)
        {
            await DenyAccessAsync("створення заправки", "Заправки реєструє лише оператор АЗС.");
            return;
        }

        await _navigation.GoToAsync("refueling/edit");
    }

    [RelayCommand]
    private async Task OpenDetailsAsync(Refueling? refueling)
    {
        if (refueling is null)
            return;

        _logger.LogDebug("Відкриття картки заправки Id={Id}", refueling.Id);

        await _navigation.GoToAsync("refueling/detail", new Dictionary<string, object>
        {
            ["SelectedRefueling"] = refueling
        });
    }

    [RelayCommand]
    private async Task DeleteRefuelingAsync(Refueling? refueling)
    {
        if (refueling is null)
            return;

        if (!CanDelete)
        {
            await DenyAccessAsync($"видалення заправки Id={refueling.Id}", "Видаляти заправки може лише бухгалтер.");
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync("Видалення заправки",
            $"Видалити заправку {refueling.Title}? Пальне буде повернено в резервуар.", "Видалити", "Скасувати");
        if (!confirmed)
            return;

        try
        {
            await _repository.DeleteAsync(refueling);
            _all.RemoveAll(r => r.Id == refueling.Id);
            ApplyFilter();
            StatusMessage = $"Заправку {refueling.Title} видалено";
            _logger.LogInformation("Заправку видалено користувачем {User}: Id={Id}", _session.UserName, refueling.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося видалити заправку Id={Id}", refueling.Id);
            await _dialogs.AlertAsync("Помилка", "Не вдалося видалити заправку.");
            return;
        }

        await RefreshPendingAsync();
        _ = _sync.SyncAsync();
    }

    [RelayCommand]
    private Task OpenTanksAsync() => _navigation.GoToAsync("tanks");

    [RelayCommand]
    private Task OpenVehiclesAsync() => _navigation.GoToAsync("vehicles");

    [RelayCommand]
    private async Task OpenReportAsync()
    {
        if (!IsAccountant)
        {
            await DenyAccessAsync("відкриття звіту", "Звіт доступний лише бухгалтеру.");
            return;
        }

        await _navigation.GoToAsync("report");
    }

    [RelayCommand]
    private async Task OpenStaffAsync()
    {
        if (!IsAccountant)
        {
            await DenyAccessAsync("відкриття розділу «Персонал»", "Розділ доступний лише бухгалтеру.");
            return;
        }

        await _navigation.GoToAsync("users");
    }

    [RelayCommand]
    private Task OpenSettingsAsync() => _navigation.GoToAsync("settings");

    [RelayCommand]
    private async Task SyncAsync()
    {
        _logger.LogInformation("Ручний запуск синхронізації");
        await _sync.SyncAsync();
        await RefreshPendingAsync();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _auth.Logout();
        await _navigation.GoToAsync("//LoginPage");
    }

    // Результат форми редагування повертається у список через параметри навігації
    public void ApplyQuery(IDictionary<string, object> query)
    {
        if (query.TryGetValue("SavedRefueling", out var value) && value is Refueling saved)
        {
            StatusMessage = $"Збережено: {saved.Title}";
            _ = LoadAsync();
        }
    }

    private async Task DenyAccessAsync(string action, string explanation)
    {
        _logger.LogWarning("Відмовлено в доступі: користувач {User} (роль {Role}) намагався виконати дію «{Action}»",
            _session.UserName, _session.CurrentRole, action);

        await _dialogs.AlertAsync("Доступ заборонено", explanation);
    }

    private async Task RefreshPendingAsync()
    {
        try
        {
            PendingCount = await _sync.GetPendingCountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося отримати розмір черги синхронізації");
        }
    }

    // fuelerp://refueling/{id}: відкриває картку заправки
    private async Task HandleDeepLinkAsync()
    {
        var link = _state.PendingDeepLink;
        if (link is null || !_isLoaded)
            return;

        _state.PendingDeepLink = null;
        _logger.LogInformation("Отримано deep link: {Link}", link);

        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri)
            || uri.Scheme != "fuelerp"
            || uri.Host != "refueling"
            || !int.TryParse(uri.AbsolutePath.Trim('/'), out var id))
        {
            _logger.LogWarning("Невідомий формат deep link: {Link}", link);
            return;
        }

        var refueling = _all.FirstOrDefault(r => r.Id == id);
        if (refueling is null)
        {
            _logger.LogWarning("Deep link: заправку Id={Id} не знайдено", id);
            return;
        }

        try
        {
            await OpenDetailsAsync(refueling);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка навігації за deep link");
        }
    }
}
