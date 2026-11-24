using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using StudentCabinetMVVM.Data;
using StudentCabinetMVVM.Models;
using StudentCabinetMVVM.Services;

namespace StudentCabinetMVVM.ViewModels;

public class StudentViewModel : INotifyPropertyChanged, IQueryAttributable
{
    private readonly IAppStateService _state;
    private readonly IStudentRepository _repository;
    private readonly ISyncService _sync;
    private readonly ISessionService _session;
    private readonly IAuthService _auth;
    private readonly ILogger<StudentViewModel> _logger;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private Student _student = new();
    private bool _isLoaded;
    private int _pendingCount;
    private (int Id, double Score)? _lastSaved;

    public string FullName
    {
        get => _student.FullName;
        set
        {
            if (_student.FullName != value)
            {
                _student.FullName = value;
                _state.DraftFullName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Greeting));
                ((Command)AddStudentCommand).ChangeCanExecute();
            }
        }
    }

    public string Group
    {
        get => _student.Group;
        set
        {
            if (_student.Group != value)
            {
                _student.Group = value;
                _state.DraftGroup = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Greeting));
            }
        }
    }

    public double AverageScore
    {
        get => _student.AverageScore;
        set
        {
            if (_student.AverageScore != value)
            {
                _student.AverageScore = value;
                _state.DraftAverageScore = value;
                OnPropertyChanged();
            }
        }
    }

    public string Greeting => $"Студент: {FullName}, група {Group}";

    public int PendingCount
    {
        get => _pendingCount;
        private set
        {
            if (_pendingCount != value)
            {
                _pendingCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PendingText));
            }
        }
    }

    public string PendingText => $"У черзі синхронізації: {PendingCount}";

    public string CurrentUserText => _session.CurrentRole is { } role
        ? $"Користувач: {_session.UserName} ({role.ToDisplayName()})"
        : "Користувач не увійшов";

    // Пункт «Керування користувачами» видно лише адміністратору (прив'язка IsVisible у MainPage.xaml)
    public bool IsAdminPanelVisible => _session.IsInRole(UserRole.Administrator);

    public ObservableCollection<Student> Students { get; } = new();

    public ICommand AddStudentCommand { get; }

    public ICommand OpenDetailsCommand { get; }

    public ICommand DeleteStudentCommand { get; }

    public ICommand LoadCommand { get; }

    public ICommand OpenSettingsCommand { get; }

    public ICommand SyncCommand { get; }

    public ICommand OpenUsersCommand { get; }

    public ICommand LogoutCommand { get; }

    public ICommand OpenInspectionsCommand { get; }

    public StudentViewModel(
        IAppStateService state,
        IStudentRepository repository,
        ISyncService sync,
        ISessionService session,
        IAuthService auth,
        ILogger<StudentViewModel> logger)
    {
        _state = state;
        _repository = repository;
        _sync = sync;
        _session = session;
        _auth = auth;
        _logger = logger;

        AddStudentCommand = new Command(async () => await AddStudentAsync(), CanAddStudent);
        OpenDetailsCommand = new Command<Student>(async (student) =>
            await OpenDetailsAsync(student));
        DeleteStudentCommand = new Command<Student>(async (student) =>
            await DeleteStudentAsync(student));
        LoadCommand = new Command(async () => await LoadAsync());
        OpenSettingsCommand = new Command(async () =>
            await Shell.Current.GoToAsync("settings"));
        SyncCommand = new Command(async () => await SyncNowAsync());
        OpenUsersCommand = new Command(async () => await OpenUsersAsync());
        LogoutCommand = new Command(async () => await LogoutAsync());
        OpenInspectionsCommand = new Command(async () => await Shell.Current.GoToAsync("inspections"));

        _session.SessionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CurrentUserText));
            OnPropertyChanged(nameof(IsAdminPanelVisible));
        };

        _state.StateRestored += (_, _) => LoadDraft();
        _state.DeepLinkReceived += async (_, _) => await HandleDeepLinkAsync();
        _sync.QueueChanged += (_, _) =>
            MainThread.BeginInvokeOnMainThread(async () => await RefreshPendingAsync());

        LoadDraft();
    }

    private void LoadDraft()
    {
        FullName = _state.DraftFullName;
        Group = _state.DraftGroup;
        AverageScore = _state.DraftAverageScore;
    }

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
            await Shell.Current.DisplayAlert("Помилка", "Не вдалося завантажити дані з бази.", "OK");
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
            await Shell.Current.DisplayAlert("Помилка", "Не вдалося зберегти студента.", "OK");
            return;
        }

        FullName = string.Empty;
        Group = string.Empty;

        await AfterChangeAsync();
    }

    private async Task DeleteStudentAsync(Student student)
    {
        if (student is null) return;

        // Другий рівень захисту: перевірка ролі в самій команді, а не лише приховування кнопки
        if (!_session.IsInRole(UserRole.Administrator))
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
            await Shell.Current.DisplayAlert("Помилка", "Не вдалося видалити студента.", "OK");
            return;
        }

        await AfterChangeAsync();
    }

    private async Task OpenUsersAsync()
    {
        if (!_session.IsInRole(UserRole.Administrator))
        {
            await DenyAccessAsync("відкриття розділу «Керування користувачами»");
            return;
        }

        await Shell.Current.GoToAsync("users");
    }

    private async Task LogoutAsync()
    {
        _auth.Logout();
        await Shell.Current.GoToAsync("//LoginPage");
    }

    private async Task DenyAccessAsync(string action)
    {
        _logger.LogWarning("Відмовлено в доступі: користувач {User} (роль {Role}) намагався виконати дію «{Action}»",
            _session.UserName, _session.CurrentRole, action);

        await Shell.Current.DisplayAlert("Доступ заборонено",
            "Дія доступна лише адміністратору.", "OK");
    }

    private async Task OpenDetailsAsync(Student student)
    {
        if (student is null) return;

        _logger.LogDebug("Відкриття деталей студента Id={Id}", student.Id);

        var parameters = new Dictionary<string, object>
        {
            { "SelectedStudent", student }
        };
        await Shell.Current.GoToAsync("studentdetail", parameters);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("UpdatedStudent", out var value)
            && value is Student updated)
        {
            _ = SaveUpdatedAsync(updated);
        }
    }

    private async Task SaveUpdatedAsync(Student updated)
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
            await Shell.Current.DisplayAlert("Помилка", "Не вдалося зберегти зміни.", "OK");
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

    private async Task SyncNowAsync()
    {
        _logger.LogInformation("Ручний запуск синхронізації");
        await _sync.SyncAsync();
        await RefreshPendingAsync();
    }

    private async Task HandleDeepLinkAsync()
    {
        var link = _state.PendingDeepLink;
        if (link is null || !_isLoaded || Shell.Current?.CurrentPage is null) return;

        _state.PendingDeepLink = null;
        _logger.LogInformation("Отримано deep link: {Link}", link);

        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri)
            || uri.Scheme != "studentcabinet"
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

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
