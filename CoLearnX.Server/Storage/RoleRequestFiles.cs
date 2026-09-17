namespace CoLearnX.Server.Storage;

internal static class RoleRequestFiles
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public const long MaxRequestBytes = (MaxBytes * 2) + (1024 * 1024);

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".png", ".jpg", ".jpeg",
    };

    public static string RequireSafeExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext) || !Allowed.Contains(ext))
            throw new InvalidOperationException("Allowed types: PDF, DOCX, PNG, JPG.");
        return ext.ToLowerInvariant();
    }

    public static string ContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream",
    };

    public static string DownloadName(string label, string storageKey)
    {
        var ext = Path.GetExtension(storageKey);
        return label + ext;
    }
}
