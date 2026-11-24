using Microsoft.Extensions.Logging;

namespace FuelErp.Services;

public class ShakeDetectionService : IShakeDetectionService
{
    private readonly IAccelerometer _accelerometer;
    private readonly ILogger<ShakeDetectionService> _logger;

    public ShakeDetectionService(IAccelerometer accelerometer, ILogger<ShakeDetectionService> logger)
    {
        _accelerometer = accelerometer;
        _logger = logger;
    }

    public event EventHandler? ShakeDetected;

    public bool IsSupported => _accelerometer.IsSupported;

    public bool IsMonitoring => _accelerometer.IsMonitoring;

    public void Start()
    {
        if (!_accelerometer.IsSupported)
        {
            _logger.LogWarning("Акселерометр не підтримується на цьому пристрої: струшування недоступне");
            return;
        }

        if (_accelerometer.IsMonitoring)
            return;

        try
        {
            _accelerometer.ShakeDetected += OnShakeDetected;
            _accelerometer.Start(SensorSpeed.Game);
            _logger.LogDebug("Акселерометр запущено");
        }
        catch (Exception ex)
        {
            _accelerometer.ShakeDetected -= OnShakeDetected;
            _logger.LogError(ex, "Не вдалося запустити акселерометр");
        }
    }

    public void Stop()
    {
        if (!_accelerometer.IsMonitoring)
            return;

        try
        {
            _accelerometer.ShakeDetected -= OnShakeDetected;
            _accelerometer.Stop();
            _logger.LogDebug("Акселерометр зупинено");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося зупинити акселерометр");
        }
    }

    // Подія приходить із потоку сенсора, тому оновлення інтерфейсу виконується у ViewModel через MainThread
    private void OnShakeDetected(object? sender, EventArgs e) => ShakeDetected?.Invoke(this, EventArgs.Empty);
}
