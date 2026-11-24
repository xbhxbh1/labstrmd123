using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FuelErp.Models;
using FuelErp.Services;

namespace FuelErp.ViewModels;

public partial class StudentDetailViewModel : ObservableObject, IQueryReceiver
{
    private readonly INavigationService _navigation;
    private Student? _student;

    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _group = string.Empty;

    [ObservableProperty]
    private double _averageScore;

    public StudentDetailViewModel(INavigationService navigation)
    {
        _navigation = navigation;
    }

    public void ApplyQuery(IDictionary<string, object> query)
    {
        if (query.TryGetValue("SelectedStudent", out var value) && value is Student student)
        {
            _student = student;
            FullName = student.FullName;
            Group = student.Group;
            AverageScore = student.AverageScore;
        }
    }

    [RelayCommand]
    private Task GoBackAsync() => _navigation.GoBackAsync();

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_student is null)
            return;

        _student.AverageScore = AverageScore;

        await _navigation.GoBackAsync(new Dictionary<string, object>
        {
            ["UpdatedStudent"] = _student
        });
    }
}
