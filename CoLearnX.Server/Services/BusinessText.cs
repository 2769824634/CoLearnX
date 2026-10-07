namespace CoLearnX.Server.Services;

internal static class BusinessText
{
    public static string Credits(int amount)
        => $"{amount} {(Math.Abs((long)amount) == 1 ? "credit" : "credits")}";
}
