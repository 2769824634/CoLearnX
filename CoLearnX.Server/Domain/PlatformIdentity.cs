namespace CoLearnX.Server.Domain;

public static class PlatformIdentity
{
    public const string Email = "colearnx@system.colearnx";
    public const string Name = "CoLearnX";

    public static bool IsInternalEmail(string email)
        => email.EndsWith("@staff.colearnx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(email, Email, StringComparison.OrdinalIgnoreCase);
}
