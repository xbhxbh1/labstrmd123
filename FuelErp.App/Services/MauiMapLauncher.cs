namespace FuelErp.Services;

public class MauiMapLauncher : IMapLauncher
{
    private readonly IMap _map;
    private readonly IDeviceInfo _deviceInfo;

    public MauiMapLauncher(IMap map, IDeviceInfo deviceInfo)
    {
        _map = map;
        _deviceInfo = deviceInfo;
    }

    // Елемент Map не підтримується у Windows
    public bool SupportsEmbeddedMap => _deviceInfo.Platform != DevicePlatform.WinUI;

    public Task OpenAsync(double latitude, double longitude, string name) =>
        _map.OpenAsync(latitude, longitude, new MapLaunchOptions { Name = name });
}
