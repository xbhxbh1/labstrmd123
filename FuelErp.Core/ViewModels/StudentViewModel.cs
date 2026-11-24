using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using FuelErp.Data;
using FuelErp.Models;
using FuelErp.Services;

namespace FuelErp.ViewModels;

public partial class StudentViewModel : ObservableObject, IQueryReceiver
{
    private readonly IAppStateService _state;
    private readonly IStudentRepository _repository;
    private readonly ISyncService _sync;
    private readonly ISessionService _session;
    private readonly IAuthService _auth;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IMainThreadService _mainThread;
    private readonly ILogger<StudentViewModel> _logger;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private bool _isLoaded;
    private (int Id, double Score)? _lastSaved;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Greeting))]
    [NotifyCanExecuteChangedFor(nameof(AddStudentCommand))]
    private string _fullName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Greeting))]
    private string _group = string.Empty;

    [ObservableProperty]
    private double _averageScore;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingText))]
    private int _pendingCount;

    public string Greeting => $"Студент: {FullName}, група {Group}";

    public string PendingText => $"У черзі синхронізації: {PendingCount}";

    public string CurrentUserText => _session.CurrentRole is { } role
        ? $"Користувач: {_session.UserName} ({role.ToDisplayName()})"
        : "Користувач не увійшов";

    // Пункт «Керування користувачами» видно лише адміністратору (прив'язка IsVisible у MainPage.xaml)
    public bool IsAdminPanelVisible => _session.IsInRole(UserRole.Accountant);

    public ObservableCollection<Student> Students { get; } = new();

    public StudentViewModel(
        IAppStateService state,
        IStudentRepository repository,
        ISyncService sync,
        ISessionService session,
        IAuthService auth,
        INavigationService navigation,
        IDialogService dialogs,
        IMainThreadService mainThread,
        ILogger<StudentViewModel> logger)
    {
        _state = state;
        _repository = repository;
        _sync = sync;
        _session = session;
        _auth = auth;
        _navigation = navigation;
        _dialogs = dialogs;
        _mainThread = mainThread;
        _logger = logger;

        _session.SessionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CurrentUserText));
            OnPropertyChanged(nameof(IsAdminPanelVisible));
        };

        _state.StateRestored += (_, _) => LoadDraft();
        _state.DeepLinkReceived += async (_, _) => await HandleDeepLinkAsync();
        _sync.QueueChanged += (_, _) =>
            _mainThread.BeginInvokeOnMainThread(async () => await RefreshPendingAsync());

        LoadDraft();
    }

    // Чернетка форми зберігається в стані застосунку (лабораторна робота 3)
    partial void OnFullNameChanged(string value) => _state.DraftFullName = value;

    partial void OnGroupChanged(string value) => _state.DraftGroup = value;

    partial void OnAverageScoreChanged(double value) => _state.DraftAverageScore = value;

    private void LoadDraft()
    {
        FullName = _state.DraftFullName;
        Group = _state.DraftGroup;
        AverageScore = _state.DraftAverageScore;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await _loadLock.WaitAsync();
        try
        {
            if (!_isLoaded)
            {
                _logger.LogInformation("Завантаження списку студентів із бази даних");
                var students = await _repository.GetAllAsync();

                Students.Clear();
                foreach (var student in students)
                    Students.Add(student);

                _isLoaded = true;
                _logger.LogInformation("Завантажено студентів: {Count}", students.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося завантажити список студентів");
            await _dialogs.AlertAsync("Помилка", "Не вдалося завантажити дані з бази.");
            return;
        }
        finally
        {
            _loadLock.Release();
        }

        await RefreshPendingAsync();
        await HandleDeepLinkAsync();
    }

    private bool CanAddStudent() => !string.IsNullOrWhiteSpace(FullName);

    [RelayCommand(CanExecute = nameof(CanAddStudent))]
    private async Task AddStudentAsync()
    {
        if (string.IsNullOrWhiteSpace(Group))
            _logger.LogWarning("Студента додається без вказання групи");

        var student = new Student
        {
            FullName = FullName,
            Group = Group,
            AverageScore = AverageScore
        };

        try
        {
            await _repository.SaveAsync(student);
            Students.Add(student);
            _logger.LogInformation("Студента додано до списку: Id={Id}", student.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося додати студента");
            await _dialogs.AlertAsync("Помилка", "Не вдалося зберегти студента.");
            return;
        }

        FullName = string.Empty;
        Group = string.Empty;

        await AfterChangeAsync();
    }

    [RelayCommand]
    private async Task DeleteStudentAsync(Student? student)
    {
        if (student is null) return;

        // Другий рівень захисту: перевірка ролі в самій команді, а не лише приховування кнопки
        if (!_session.IsInRole(UserRole.Accountant))
        {
            await DenyAccessAsync($"видалення студента Id={student.Id}");
            return;
        }

        try
        {
            await _repository.DeleteAsync(student);
            Students.Remove(student);
            _logger.LogInformation("Студента видалено зі списку: Id={Id}", student.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося видалити студента Id={Id}", student.Id);
            await _dialogs.AlertAsync("Помилка", "Не вдалося видалити студента.");
            return;
        }

        await AfterChangeAsync();
    }

    [RelayCommand]
    private async Task OpenUsersAsync()
    {
        if (!_session.IsInRole(UserRole.Accountant))
        {
            await DenyAccessAsync("відкриття розділу «Керування користувачами»");
            return;
        }

        await _navigation.GoToAsync("users");
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _auth.Logout();
        await _navigation.GoToAsync("//LoginPage");
    }

    [RelayCommand]
    private Task OpenSettingsAsync() => _navigation.GoToAsync("settings");

    [RelayCommand]
    private Task OpenInspectionsAsync() => _navigation.GoToAsync("inspections");

    [RelayCommand]
    private async Task SyncAsync()
    {
        _logger.LogInformation("Ручний запуск синхронізації");
        await _sync.SyncAsync();
        await RefreshPendingAsync();
    }

    private async Task DenyAccessAsync(string action)
    {
        _logger.LogWarning("Відмовлено в доступі: користувач {User} (роль {Role}) намагався виконати дію «{Action}»",
            _session.UserName, _session.CurrentRole, action);

        await _dialogs.AlertAsync("Доступ заборонено", "Дія доступна лише адміністратору.");
    }

    [RelayCommand]
    private async Task OpenDetailsAsync(Student? student)
    {
        if (student is null) return;

        _logger.LogDebug("Відкриття деталей студента Id={Id}", student.Id);

        await _navigation.GoToAsync("studentdetail", new Dictionary<string, object>
        {
            ["SelectedStudent"] = student
        });
    }

    // Параметри навігації надходять зі сторінки (IQueryAttributable) і пересилаються сюди
    public void ApplyQuery(IDictionary<string, object> query)
    {
        if (query.TryGetValue("UpdatedStudent", out var value) && value is Student updated)
            _ = ApplyUpdatedStudentAsync(updated);
    }

    public async Task ApplyUpdatedStudentAsync(Student updated)
    {
        if (_lastSaved == (updated.Id, updated.AverageScore))
            return;

        _lastSaved = (updated.Id, updated.AverageScore);

        try
        {
            await _repository.SaveAsync(updated);

            var index = Students.IndexOf(updated);
            if (index >= 0)
            {
                Students.RemoveAt(index);
                Students.Insert(index, updated);
            }

            _logger.LogInformation("Оновлено студента Id={Id}, середній бал {Score:F2}",
                updated.Id, updated.AverageScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося зберегти зміни студента Id={Id}", updated.Id);
            await _dialogs.AlertAsync("Помилка", "Не вдалося зберегти зміни.");
            return;
        }

        await AfterChangeAsync();
    }

    private async Task AfterChangeAsync()
    {
        await RefreshPendingAsync();
        _ = _sync.SyncAsync();
    }

    private async Task RefreshPendingAsync()
    {
        PendingCount = await _sync.GetPendingCountAsync();
    }

    private async Task HandleDeepLinkAsync()
    {
        var link = _state.PendingDeepLink;
        if (link is null || !_isLoaded) return;

        _state.PendingDeepLink = null;
        _logger.LogInformation("Отримано deep link: {Link}", link);

        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri)
            || uri.Scheme != "fuelerp"
            || uri.Host != "student")
        {
            _logger.LogWarning("Невідомий формат deep link: {Link}", link);
            return;
        }

        var name = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));
        var student = Students.FirstOrDefault(s =>
            string.Equals(s.FullName, name, StringComparison.OrdinalIgnoreCase));

        if (student is null)
        {
            _logger.LogWarning("Deep link: студента не знайдено ({Name})", name);
            return;
        }

        try
        {
            await Task.Delay(300);
            await OpenDetailsAsync(student);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка навігації за deep link");
        }
    }
}
