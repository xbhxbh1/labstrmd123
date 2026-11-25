using Microsoft.Extensions.Logging;

namespace FuelErp.Services;

public class PhotoService : IPhotoService
{
    private readonly IMediaPicker _mediaPicker;
    private readonly IPermissionService _permissions;
    private readonly ILogger<PhotoService> _logger;

    public PhotoService(IMediaPicker mediaPicker, IPermissionService permissions, ILogger<PhotoService> logger)
    {
        _mediaPicker = mediaPicker;
        _permissions = permissions;
        _logger = logger;
    }

    public Task<PhotoResult> TakePhotoAsync(string label) => RunAsync(label, capture: true);

    public Task<PhotoResult> PickPhotoAsync(string label) => RunAsync(label, capture: false);

    private async Task<PhotoResult> RunAsync(string label, bool capture)
    {
        var action = capture ? "зйомку" : "вибір фото з галереї";

        try
        {
            if (capture)
            {
                if (!_mediaPicker.IsCaptureSupported)
                {
                    _logger.LogWarning("Камера недоступна на цьому пристрої (IsCaptureSupported = false)");
                    return new PhotoResult(PhotoStatus.NotSupported);
                }

                if (!await _permissions.EnsureAsync(DevicePermission.Camera))
                {
                    _logger.LogWarning("Фото не зроблено: користувач не надав дозвіл на камеру");
                    return new PhotoResult(PhotoStatus.PermissionDenied);
                }
            }

            var photo = capture
                ? await _mediaPicker.CapturePhotoAsync()
                : await _mediaPicker.PickPhotoAsync();

            if (photo is null)
            {
                _logger.LogInformation("Користувач скасував {Action}", action);
                return new PhotoResult(PhotoStatus.Cancelled);
            }

            var path = await CopyToAppDataAsync(photo, label);
            _logger.LogInformation("Фото скопійовано в каталог застосунку: {FileName}", Path.GetFileName(path));
            return new PhotoResult(PhotoStatus.Success, path);
        }
        catch (FeatureNotSupportedException ex)
        {
            _logger.LogWarning(ex, "Функція камери або галереї не підтримується пристроєм");
            return new PhotoResult(PhotoStatus.NotSupported);
        }
        catch (PermissionException ex)
        {
            _logger.LogWarning(ex, "Система відмовила в доступі до камери або фото");
            return new PhotoResult(PhotoStatus.PermissionDenied);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося отримати фото ({Action})", action);
            return new PhotoResult(PhotoStatus.Failed);
        }
    }

    // У базі зберігається лише шлях до файла, а не самі байти зображення
    private static async Task<string> CopyToAppDataAsync(FileResult photo, string label)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safeRoom = new string(label.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        if (safeRoom.Length == 0)
            safeRoom = "unknown";

        var extension = Path.GetExtension(photo.FileName);
        if (string.IsNullOrEmpty(extension))
            extension = ".jpg";

        var directory = Path.Combine(FileSystem.AppDataDirectory, "photos");
        Directory.CreateDirectory(directory);

        var target = Path.Combine(directory, $"receipt_{safeRoom}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");

        await using var source = await photo.OpenReadAsync();
        await using var destination = File.Create(target);
        await source.CopyToAsync(destination);

        return target;
    }
}
