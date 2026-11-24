namespace StudentCabinetMVVM.Services;

public interface IMapLauncher
{
    /// <summary>Чи підтримує платформа вбудований елемент Map (у Windows - ні).</summary>
    bool SupportsEmbeddedMap { get; }

    /// <summary>Відкриває координати у зовнішньому застосунку карт.</summary>
    Task OpenAsync(double latitude, double longitude, string name);
}
