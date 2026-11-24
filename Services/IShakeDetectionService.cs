namespace StudentCabinetMVVM.Services;

/// <summary>Реакція на струшування пристрою (акселерометр). Сенсор слід запускати й зупиняти разом зі сторінкою.</summary>
public interface IShakeDetectionService
{
    bool IsSupported { get; }

    bool IsMonitoring { get; }

    event EventHandler? ShakeDetected;

    void Start();

    void Stop();
}
