namespace FuelErp.Models;

/// <summary>
/// Акт огляду аудиторії (відповідає WagonInspection із методички): номер аудиторії, дата,
/// опис дефекту, шлях до фото та координати місця огляду.
/// </summary>
public class RoomInspection
{
    public int Id { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public DateTime InspectedAt { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>Шлях до файла в AppDataDirectory (у базі зберігається лише шлях, а не байти зображення).</summary>
    public string? PhotoPath { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public bool HasPhoto => !string.IsNullOrEmpty(PhotoPath);

    public bool HasLocation => Latitude.HasValue && Longitude.HasValue;

    public string LocationText => HasLocation
        ? $"Широта: {Latitude:F5}, довгота: {Longitude:F5}"
        : "Координати не визначено";
}
