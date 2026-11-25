namespace FuelErp.Models;

public enum TankLevelState
{
    Normal,
    Low,
    Critical
}

/// <summary>Резервуар АЗС із паливом або мастильними матеріалами (Резервуар).</summary>
public class FuelTank
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public FuelType FuelType { get; set; }

    public double CapacityLiters { get; set; }

    public double CurrentLiters { get; set; }

    /// <summary>Заповненість від 0 до 1 (для індикатора рівня).</summary>
    public double FillFraction => CapacityLiters <= 0 ? 0 : Math.Clamp(CurrentLiters / CapacityLiters, 0, 1);

    /// <summary>Стан рівня для візуальної індикації: менше 10 % - критичний, менше 25 % - низький.</summary>
    public TankLevelState LevelState =>
        FillFraction < 0.10 ? TankLevelState.Critical
        : FillFraction < 0.25 ? TankLevelState.Low
        : TankLevelState.Normal;

    public string LevelText => $"{CurrentLiters:N0} / {CapacityLiters:N0} л ({FillFraction:P0})";

    public string DisplayName => $"{Name} ({FuelType.ToDisplayName()})";
}
