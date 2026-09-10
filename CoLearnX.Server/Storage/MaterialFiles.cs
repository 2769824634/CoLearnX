namespace CoLearnX.Server.Storage;

internal static class MaterialFiles
{
    public const long MaxBytes = 20 * 1024 * 1024;
    public const long MaxRequestBytes = MaxBytes + 1024 * 1024;

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".pptx", ".docx", ".png", ".jpg", ".jpeg",
    };

    public static string RequireSafeExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext) || !Allowed.Contains(ext))
            throw new InvalidOperationException("Allowed types: PDF, PPTX, DOCX, PNG, JPG.");
        return ext.ToLowerInvariant();
    }

    public static string FormatLabel(string extension) => extension.TrimStart('.').ToUpperInvariant() switch
    {
        "JPEG" => "JPG",
        var label => label,
    };

    public static string ContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream",
    };

    public static string DownloadName(string title, string storageKey)
    {
        var ext = Path.GetExtension(storageKey);
        var stem = string.Join("_", title.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(stem))
            stem = "material";
        return stem + ext;
    }
}
