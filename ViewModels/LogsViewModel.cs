using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StudentCabinetMVVM.Models;
using StudentCabinetMVVM.Services;

namespace StudentCabinetMVVM.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private readonly ILogFileReader _reader;
    private readonly ICrashReporter _crashReporter;
    private readonly ILogger<LogsViewModel> _logger;

    public LogsViewModel(ILogFileReader reader, ICrashReporter crashReporter, ILogger<LogsViewModel> logger)
    {
        _reader = reader;
        _crashReporter = crashReporter;
        _logger = logger;
        Dsn = crashReporter.Dsn;
    }

    public IReadOnlyList<string> Levels { get; } = LogParser.LevelNames;

    public IReadOnlyList<int> Counts { get; } = new[] { 20, 50, 100, 200 };

    public ObservableCollection<LogEntry> Entries { get; } = new();

    [ObservableProperty]
    private string _selectedLevel = "Debug";

    [ObservableProperty]
    private int _selectedCount = 50;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _dsn = string.Empty;

    partial void OnSelectedLevelChanged(string value) => RefreshCommand.Execute(null);

    partial void OnSelectedCountChanged(int value) => RefreshCommand.Execute(null);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var entries = await _reader.ReadLastAsync(SelectedCount, SelectedLevel);

            Entries.Clear();
            foreach (var entry in entries.Reverse())
                Entries.Add(entry);

            Status = $"Показано записів: {Entries.Count} (рівень {SelectedLevel} і вище)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося прочитати файл журналу");
            Status = "Не вдалося прочитати журнал";
        }
    }

    [RelayCommand]
    private async Task SimulateTaskExceptionAsync()
    {
        _logger.LogWarning("Моделювання необробленого винятку у фоновій задачі");

        _ = Task.Run(() =>
        {
            throw new InvalidOperationException("Змодельований необроблений виняток у фоновій задачі");
        });

        await Task.Delay(500);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(500);

        await RefreshAsync();
        Status = "Виняток змодельовано: шукайте запис «Необроблений виняток у Task» (рівень Error)";
    }

    [RelayCommand]
    private async Task SimulateCrashAsync()
    {
        var confirmed = await Shell.Current.DisplayAlert(
            "Штучний краш", "Застосунок аварійно завершить роботу. Продовжити?", "Так", "Ні");
        if (!confirmed)
            return;

        _logger.LogWarning("Користувач ініціював штучний краш застосунку");

        var thread = new Thread(() => throw new InvalidOperationException("Штучний краш: необроблений виняток у потоці"));
        thread.Start();
    }

    [RelayCommand]
    private async Task SendTestReportAsync()
    {
        var id = _crashReporter.SendTestReport();
        if (id is null)
        {
            Status = "Сервіс збору крашів вимкнено: введіть DSN, збережіть його і перезапустіть застосунок";
            return;
        }

        await _crashReporter.FlushAsync();
        Status = $"Тестовий звіт надіслано в Sentry (EventId {id})";
    }

    [RelayCommand]
    private void SaveDsn()
    {
        _crashReporter.SaveDsn(Dsn);
        Status = "DSN збережено. Перезапустіть застосунок, щоб підключити сервіс";
    }
}
