namespace CoLearnX.Server.Storage;

// Config section name is "Storage" (user-secrets / appsettings)
public class StorageOptions
{
    public const string SectionName = "Storage";

    public string ConnectionString { get; set; } = string.Empty;
    public string Container { get; set; } = "materials";
    public string RootPath { get; set; } = string.Empty;

    public bool UseAzure => !string.IsNullOrWhiteSpace(ConnectionString);
}
