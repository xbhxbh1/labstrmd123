namespace StudentCabinetMVVM.Models;

public enum UserRole
{
    Teacher,
    Administrator
}

public static class UserRoleExtensions
{
    public static string ToDisplayName(this UserRole role) => role switch
    {
        UserRole.Administrator => "Адміністратор",
        UserRole.Teacher => "Викладач",
        _ => role.ToString()
    };
}
