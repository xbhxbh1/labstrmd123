using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StudentCabinetMVVM.Data;
using StudentCabinetMVVM.Models;
using StudentCabinetMVVM.Services;

namespace StudentCabinetMVVM.ViewModels;

public partial class InspectionsViewModel : ObservableObject
{
    private readonly IInspectionRepository _repository;
    private readonly IMapLauncher _map;
    private readonly INetworkStatusService _network;
    private readonly ISyncService _sync;
    private readonly IShakeDetectionService _shake;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IMainThreadService _mainThread;
    private readonly ILogger<InspectionsViewModel> _logger;

    public ObservableCollection<RoomInspection> Inspections { get; } = new();

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NetworkText))]
    private bool _isOnline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingText))]
    private int _pendingCount;

    public string NetworkText => IsOnline ? "● Онлайн" : "● Офлайн: зміни збережуться в черзі";

    public string PendingText => $"У черзі синхронізації: {PendingCount}";

    // Кнопка імітації потрібна лише на пристроях без акселерометра (наприклад, ноутбук)
    public bool ShowShakeSimulation => !_shake.IsSupported;

    public InspectionsViewModel(
        IInspectionRepository repository,
        IMapLauncher map,
        INetworkStatusService network,
        ISyncService sync,
        IShakeDetectionService shake,
        INavigationService navigation,
        IDialogService dialogs,
        IMainThreadService mainThread,
        ILogger<InspectionsViewModel> logger)
    {
        _repository = repository;
        _map = map;
        _network = network;
        _sync = sync;
        _shake = shake;
        _navigation = navigation;
        _dialogs = dialogs;
        _mainThread = mainThread;
        _logger = logger;
    }

    // Життєвий цикл сторінки: сенсор і підписки запускаються в OnAppearing і зупиняються в OnDisappearing,
    // інакше акселерометр і далі споживав би заряд батареї, коли сторінку вже закрито
    public async Task OnAppearingAsync()
    {
        IsOnline = _network.IsOnline;

        _network.OnlineChanged += OnOnlineChanged;
        _sync.QueueChanged += OnQueueChanged;
        _shake.ShakeDetected += OnShakeDetected;
        _shake.Start();

        await LoadAsync();
        await RefreshPendingAsync();
    }

    public void OnDisappearing()
    {
        _network.OnlineChanged -= OnOnlineChanged;
        _sync.QueueChanged -= OnQueueChanged;
        _shake.ShakeDetected -= OnShakeDetected;
        _shake.Stop();
    }

    // Обробники подій сенсора й мережі викликаються не з потоку інтерфейсу,
    // тому зміна властивостей і навігація виконуються через головний потік
    private void OnOnlineChanged(object? sender, bool online) =>
        _mainThread.BeginInvokeOnMainThread(() =>
        {
            IsOnline = online;
            _logger.LogInformation("Стан мережі змінився: онлайн = {Online}", online);
        });

    private void OnQueueChanged(object? sender, EventArgs e) =>
        _mainThread.BeginInvokeOnMainThread(async () => await RefreshPendingAsync());

    private void OnShakeDetected(object? sender, EventArgs e) =>
        _mainThread.BeginInvokeOnMainThread(async () => await OpenIncidentAsync());

    private async Task OpenIncidentAsync()
    {
        _logger.LogInformation("Струшування: відкриття форми повідомлення про інцидент");

        await _navigation.GoToAsync("incident/new", new Dictionary<string, object>
        {
            ["IsIncident"] = true
        });
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

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var items = await _repository.GetAllAsync();

            Inspections.Clear();
            foreach (var item in items)
                Inspections.Add(item);

            Status = $"Актів огляду: {Inspections.Count}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося завантажити акти огляду");
            await _dialogs.AlertAsync("Помилка", "Не вдалося завантажити акти огляду.");
        }
    }

    [RelayCommand]
    private Task SimulateShakeAsync() => OpenIncidentAsync();

    [RelayCommand]
    private Task NewInspectionAsync() => _navigation.GoToAsync("inspection/new");

    // Елемент Map не підтримується у Windows: там координати відкриваються кнопкою «На карті» біля акта
    [RelayCommand]
    private async Task OpenMapAsync()
    {
        if (!_map.SupportsEmbeddedMap)
        {
            _logger.LogInformation("Вбудована карта недоступна на цій платформі, запропоновано зовнішній застосунок карт");
            await _dialogs.AlertAsync("Карта",
                "Вбудований елемент карти у Windows не підтримується. Натисніть «На карті» біля потрібного акта: " +
                "його координати відкриються у застосунку «Карти».");
            return;
        }

        await _navigation.GoToAsync("inspections/map");
    }

    [RelayCommand]
    private async Task OpenDetailsAsync(RoomInspection? inspection)
    {
        if (inspection is null)
            return;

        await _navigation.GoToAsync("inspection/detail", new Dictionary<string, object>
        {
            ["SelectedInspection"] = inspection
        });
    }

    [RelayCommand]
    private async Task ShowOnMapAsync(RoomInspection? inspection)
    {
        if (inspection is null)
            return;

        if (!inspection.HasLocation)
        {
            await _dialogs.AlertAsync("Немає координат", "Для цього акта координати не визначались.");
            return;
        }

        try
        {
            await _map.OpenAsync(inspection.Latitude!.Value, inspection.Longitude!.Value,
                $"Акт огляду, аудиторія {inspection.RoomNumber}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося відкрити застосунок карт");
            await _dialogs.AlertAsync("Карти недоступні", "Не вдалося відкрити застосунок карт.");
        }
    }
}
