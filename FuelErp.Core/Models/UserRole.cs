namespace FuelErp.Models;

public enum UserRole
{
    StationOperator,
    Accountant
}

public static class UserRoleExtensions
{
    public static string ToDisplayName(this UserRole role) => role switch
    {
        UserRole.Accountant => "Бухгалтер",
        UserRole.StationOperator => "Оператор АЗС",
        _ => role.ToString()
    };
}
