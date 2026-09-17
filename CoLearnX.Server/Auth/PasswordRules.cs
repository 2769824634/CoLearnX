using System.Text.RegularExpressions;

namespace CoLearnX.Server.Auth;

public static class PasswordRules
{
    public const int MinLength = 10;
    public const int MaxLength = 72;
    public const string Hint =
        "Use 10–72 characters with uppercase, lowercase, a number, and a symbol.";
    public const string Pattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{10,72}$";
    public const string TakenMessage =
        "This account could not be created. Try a different email or sign in.";

    public static bool Meets(string? password, string? email = null)
    {
        if (string.IsNullOrEmpty(password)
            || password.Length < MinLength
            || password.Length > MaxLength
            || !Regex.IsMatch(password, Pattern))
            return false;

        if (string.IsNullOrWhiteSpace(email)) return true;
        var normalized = email.Trim();
        var local = normalized.Split('@')[0];
        return password.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) < 0
            && (local.Length < 3 || password.IndexOf(local, StringComparison.OrdinalIgnoreCase) < 0);
    }
}
