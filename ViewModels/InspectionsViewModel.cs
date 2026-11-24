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
    private readonly IMap _map;
    private readonly IDeviceInfo _deviceInfo;
    private readonly IConnectivity _connectivity;
    private readonly ISyncService _sync;
    private readonly IShakeDetectionService _shake;
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
        IMap map,
        IDeviceInfo deviceInfo,
        IConnectivity connectivity,
        ISyncService sync,
        IShakeDetectionService shake,
        ILogger<InspectionsViewModel> logger)
    {
        _repository = repository;
        _map = map;
        _deviceInfo = deviceInfo;
        _connectivity = connectivity;
        _sync = sync;
        _shake = shake;
        _logger = logger;
    }

    // Життєвий цикл сторінки: сенсор і підписки запускаються в OnAppearing і зупиняються в OnDisappearing,
    // інакше акселерометр і далі споживав би заряд батареї, коли сторінку вже закрито
    public async Task OnAppearingAsync()
    {
        IsOnline = _connectivity.NetworkAccess == NetworkAccess.Internet;

        _connectivity.ConnectivityChanged += OnConnectivityChanged;
        _sync.QueueChanged += OnQueueChanged;
        _shake.ShakeDetected += OnShakeDetected;
        _shake.Start();

        await LoadAsync();
        await RefreshPendingAsync();
    }

    public void OnDisappearing()
    {
        _connectivity.ConnectivityChanged -= OnConnectivityChanged;
        _sync.QueueChanged -= OnQueueChanged;
        _shake.ShakeDetected -= OnShakeDetected;
        _shake.Stop();
    }

    // Обробники подій сенсора й мережі викликаються не з потоку інтерфейсу,
    // тому зміна властивостей і навігація виконуються через MainThread
    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsOnline = e.NetworkAccess == NetworkAccess.Internet;
            _logger.LogInformation("Стан мережі змінився: {Access}", e.NetworkAccess);
        });

    private void OnQueueChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(async () => await RefreshPendingAsync());

    private void OnShakeDetected(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(async () => await OpenIncidentAsync());

    private async Task OpenIncidentAsync()
    {
        _logger.LogInformation("Струшування: відкриття форми повідомлення про інцидент");

        await Shell.Current.GoToAsync("incident/new", new Dictionary<string, object>
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
            await Shell.Current.DisplayAlert("Помилка", "Не вдалося завантажити акти огляду.", "OK");
        }
    }

    [RelayCommand]
    private Task SimulateShakeAsync() => OpenIncidentAsync();

    [RelayCommand]
    private Task NewInspectionAsync() => Shell.Current.GoToAsync("inspection/new");

    // Елемент Map не підтримується у Windows: там координати відкриваються кнопкою «На карті» біля акта
    [RelayCommand]
    private async Task OpenMapAsync()
    {
        if (_deviceInfo.Platform == DevicePlatform.WinUI)
        {
            _logger.LogInformation("Вбудована карта недоступна у Windows, запропоновано зовнішній застосунок карт");
            await Shell.Current.DisplayAlert("Карта",
                "Вбудований елемент карти у Windows не підтримується. Натисніть «На карті» біля потрібного акта: " +
                "його координати відкриються у застосунку «Карти».", "OK");
            return;
        }

        await Shell.Current.GoToAsync("inspections/map");
    }

    [RelayCommand]
    private async Task OpenDetailsAsync(RoomInspection? inspection)
    {
        if (inspection is null)
            return;

        await Shell.Current.GoToAsync("inspection/detail", new Dictionary<string, object>
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
            await Shell.Current.DisplayAlert("Немає координат", "Для цього акта координати не визначались.", "OK");
            return;
        }

        try
        {
            await _map.OpenAsync(inspection.Latitude!.Value, inspection.Longitude!.Value, new MapLaunchOptions
            {
                Name = $"Акт огляду, аудиторія {inspection.RoomNumber}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося відкрити застосунок карт");
            await Shell.Current.DisplayAlert("Карти недоступні", "Не вдалося відкрити застосунок карт.", "OK");
        }
    }
}
