namespace CoLearnX.Server.Storage;

// Limits for documents a Member hands in for an Assessment. File types reuse MaterialFiles.
internal static class AssignmentFiles
{
    public const int MaxFiles = 5;
    public const long MaxBytes = 10 * 1024 * 1024;
    // Stays under the 28.6 MB request cap that IIS applies by default on Windows hosting.
    public const long MaxTotalBytes = 25 * 1024 * 1024;
    public const long MaxRequestBytes = MaxTotalBytes + (1024 * 1024);

    public static string CleanFileName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        name = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(name)) name = "assignment";
        if (name.Length <= 200) return name;
        var extension = Path.GetExtension(name);
        return name[..(200 - extension.Length)] + extension;
    }
}
