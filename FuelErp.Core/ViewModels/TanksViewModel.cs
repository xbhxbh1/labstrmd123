using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using FuelErp.Data;
using FuelErp.Models;
using FuelErp.Services;

namespace FuelErp.ViewModels;

/// <summary>Резервуари з індикацією залишку (нормальний, низький, критичний рівень).</summary>
public partial class TanksViewModel : ObservableObject
{
    private readonly ITankRepository _repository;
    private readonly IFuelDataSeeder _seeder;
    private readonly INavigationService _navigation;
    private readonly ILogger<TanksViewModel> _logger;

    public ObservableCollection<FuelTank> Tanks { get; } = new();

    [ObservableProperty]
    private string _status = string.Empty;

    public TanksViewModel(ITankRepository repository, IFuelDataSeeder seeder, INavigationService navigation,
        ILogger<TanksViewModel> logger)
    {
        _repository = repository;
        _seeder = seeder;
        _navigation = navigation;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            await _seeder.SeedAsync();
            var tanks = await _repository.GetAllAsync();

            Tanks.Clear();
            foreach (var tank in tanks)
                Tanks.Add(tank);

            var low = tanks.Count(t => t.LevelState != TankLevelState.Normal);
            Status = low == 0 ? $"Резервуарів: {tanks.Count}" : $"Резервуарів: {tanks.Count}, потребують поповнення: {low}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося завантажити резервуари");
            Status = "Не вдалося завантажити резервуари";
        }
    }

    [RelayCommand]
    private Task GoBackAsync() => _navigation.GoBackAsync();
}
