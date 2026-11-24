using System.Text.Json;
using StudentCabinetMVVM.Models;

namespace StudentCabinetMVVM.Services;

public class AppStateService : IAppStateService
{
    private const string DraftFullNameKey = "draft_fullname";
    private const string DraftGroupKey = "draft_group";
    private const string DraftScoreKey = "draft_score";
    private const string StudentsKey = "students_json";

    private readonly ISettingsService _settings;
    private string? _pendingDeepLink;

    public AppStateService(ISettingsService settings)
    {
        _settings = settings;
    }

    public DateTime SessionStartedAt { get; } = DateTime.Now;

    public string DraftFullName { get; set; } = string.Empty;
    public string DraftGroup { get; set; } = string.Empty;
    public double DraftAverageScore { get; set; }

    public List<Student> Students { get; set; } = new();

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
        _settings.Set(DraftFullNameKey, DraftFullName);
        _settings.Set(DraftGroupKey, DraftGroup);
        _settings.Set(DraftScoreKey, DraftAverageScore);
        _settings.Set(StudentsKey, JsonSerializer.Serialize(Students));
    }

    public void Restore()
    {
        DraftFullName = _settings.Get(DraftFullNameKey, string.Empty);
        DraftGroup = _settings.Get(DraftGroupKey, string.Empty);
        DraftAverageScore = _settings.Get(DraftScoreKey, 0.0);

        var json = _settings.Get(StudentsKey, string.Empty);
        Students = string.IsNullOrEmpty(json)
            ? new List<Student>()
            : JsonSerializer.Deserialize<List<Student>>(json) ?? new List<Student>();

        StateRestored?.Invoke(this, EventArgs.Empty);
    }
}
