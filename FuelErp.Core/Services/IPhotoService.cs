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

/// <summary>Фото дефекту: зйомка камерою або вибір із галереї з копіюванням файла в каталог застосунку.</summary>
public interface IPhotoService
{
    Task<PhotoResult> TakePhotoAsync(string roomNumber);

    Task<PhotoResult> PickPhotoAsync(string roomNumber);
}
