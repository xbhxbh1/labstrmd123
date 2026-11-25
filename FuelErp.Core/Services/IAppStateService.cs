using FuelErp.Models;

namespace FuelErp.Services;

/// <summary>Стан сесії застосунку (singleton): чернетка форми й відкладене посилання deep link.</summary>
public interface IAppStateService
{
    DateTime SessionStartedAt { get; }

    /// <summary>Незбережена форма нової заправки (null, якщо чернетки немає).</summary>
    RefuelingDraft? Draft { get; set; }

    string? PendingDeepLink { get; set; }

    event EventHandler? StateRestored;
    event EventHandler? DeepLinkReceived;

    void Save();
    void Restore();
}
