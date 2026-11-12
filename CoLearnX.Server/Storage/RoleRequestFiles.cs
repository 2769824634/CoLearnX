namespace CoLearnX.Server.Storage;

internal static class RoleRequestFiles
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public const int MaxFilesPerField = 5;
    // Stays under the 28.6 MB request cap that IIS applies by default on Windows hosting.
    public const long MaxTotalBytes = 25 * 1024 * 1024;
    public const long MaxRequestBytes = MaxTotalBytes + (1024 * 1024);

    // A field with several files keeps their storage keys in one column, separated by '|'.
    // Keys are "{userId}/{guid}{ext}", so they never contain the separator.
    private const char KeySeparator = '|';

    public static string JoinKeys(IEnumerable<string> keys) => string.Join(KeySeparator, keys);

    public static IReadOnlyList<string> SplitKeys(string? value)
        => string.IsNullOrWhiteSpace(value) ? [] : value.Split(KeySeparator, StringSplitOptions.RemoveEmptyEntries);

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
