namespace CoLearnX.Server.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "CoLearnX";
    public string Audience { get; set; } = "CoLearnX.Client";
    public string SigningKey { get; set; } = "CoLearnX-Dev-Signing-Key-Change-In-Production-32+";
    public int ExpiryMinutes { get; set; } = 480;
}
