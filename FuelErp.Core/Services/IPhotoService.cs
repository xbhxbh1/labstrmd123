namespace FuelErp.Services;

public enum PhotoStatus
{
    Success,
    Cancelled,
    PermissionDenied,
    NotSupported,
    Failed
}

public record PhotoResult(PhotoStatus Status, string? Path = null);

/// <summary>Фото чека або лічильника: зйомка камерою або вибір із галереї з копіюванням файла в каталог застосунку.</summary>
public interface IPhotoService
{
    Task<PhotoResult> TakePhotoAsync(string label);

    Task<PhotoResult> PickPhotoAsync(string label);
}
