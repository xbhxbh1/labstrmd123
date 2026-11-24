namespace StudentCabinetMVVM.Services;

public interface IThemeService
{
    /// <summary>Чи використовує система темну тему (для початкового стану перемикача).</summary>
    bool IsSystemThemeDark { get; }

    void Apply(bool dark);
}
