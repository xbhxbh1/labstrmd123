using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using FuelErp.Data;
using FuelErp.Models;
using FuelErp.Services;

namespace FuelErp.ViewModels;

public record FuelReportLine(string FuelName, int Count, double Liters, double Amount)
{
    public string LitersText => $"{Liters:N1} л";

    public string AmountText => $"{Amount:N2} грн";
}

/// <summary>Звіт витрат палива за видами (лише для бухгалтера).</summary>
public partial class ReportViewModel : ObservableObject
{
    private readonly IRefuelingRepository _repository;
    private readonly ISessionService _session;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly ILogger<ReportViewModel> _logger;

    public ObservableCollection<FuelReportLine> Lines { get; } = new();

    [ObservableProperty]
    private string _totalText = string.Empty;

    public ReportViewModel(IRefuelingRepository repository, ISessionService session, INavigationService navigation,
        IDialogService dialogs, ILogger<ReportViewModel> logger)
    {
        _repository = repository;
        _session = session;
        _navigation = navigation;
        _dialogs = dialogs;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!_session.IsInRole(UserRole.Accountant))
        {
            _logger.LogWarning("Відмовлено в доступі: користувач {User} (роль {Role}) намагався відкрити звіт",
                _session.UserName, _session.CurrentRole);
            await _dialogs.AlertAsync("Доступ заборонено", "Звіт доступний лише бухгалтеру.");
            await _navigation.GoBackAsync();
            return;
        }

        try
        {
            var all = await _repository.GetAllAsync();

            Lines.Clear();
            foreach (var group in all.GroupBy(r => r.FuelType).OrderBy(g => g.Key))
            {
                Lines.Add(new FuelReportLine(group.Key.ToDisplayName(), group.Count(),
                    group.Sum(r => r.Liters), group.Sum(r => r.Total)));
            }

            TotalText = $"Усього: заправок {all.Count}, літрів {all.Sum(r => r.Liters):N1}, на суму {all.Sum(r => r.Total):N2} грн";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося сформувати звіт");
            TotalText = "Не вдалося сформувати звіт";
        }
    }

    [RelayCommand]
    private Task GoBackAsync() => _navigation.GoBackAsync();
}
