using System.Text.Json;
using FuelErp.Models;

namespace FuelErp.Services;

public class AppStateService : IAppStateService
{
    private const string DraftKey = "refueling_draft_json";

    private readonly ISettingsService _settings;
    private string? _pendingDeepLink;

    public AppStateService(ISettingsService settings)
    {
        _settings = settings;
    }

    public DateTime SessionStartedAt { get; } = DateTime.Now;

    public RefuelingDraft? Draft { get; set; }

    public string? PendingDeepLink
    {
        get => _pendingDeepLink;
        set
        {
            _pendingDeepLink = value;
            if (value is not null)
                DeepLinkReceived?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? StateRestored;
    public event EventHandler? DeepLinkReceived;

    public void Save()
    {
        var json = Draft is null || Draft.IsEmpty ? string.Empty : JsonSerializer.Serialize(Draft);
        _settings.Set(DraftKey, json);
    }

    public void Restore()
    {
        var json = _settings.Get(DraftKey, string.Empty);

        try
        {
            Draft = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<RefuelingDraft>(json);
        }
        catch (JsonException)
        {
            Draft = null;
        }

        StateRestored?.Invoke(this, EventArgs.Empty);
    }
}
