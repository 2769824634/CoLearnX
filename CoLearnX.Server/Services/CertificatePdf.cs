using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CoLearnX.Server.Services;

internal static class CertificateStages
{
    public static int ForLevel(string? level) => level?.Trim().ToLowerInvariant() switch
    {
        "intermediate" => 2,
        "advanced" => 3,
        _ => 1,
    };
}

internal static class CertificatePdf
{
    public static byte[] Create(string learnerName, string courseCode, string courseTitle, string stageName, DateTime awardedAtUtc, string verificationCode, bool isLearningPath = false)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        try
        {
            return Draw(OperatingSystem.IsWindows() ? "Microsoft YaHei" : "Segoe UI", learnerName, courseCode, courseTitle, stageName, awardedAtUtc, verificationCode, isLearningPath);
        }
        catch (Exception)
        {
            return Draw(null, learnerName, courseCode, courseTitle, stageName, awardedAtUtc, verificationCode, isLearningPath);
        }
    }

    private static byte[] Draw(string? font, string learnerName, string courseCode, string courseTitle, string stageName, DateTime awardedAtUtc, string verificationCode, bool isLearningPath)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                if (font is null)
                    page.DefaultTextStyle(style => style.FontSize(13).FontColor(Colors.BlueGrey.Darken4));
                else
                    page.DefaultTextStyle(style => style.FontFamily(font).FontSize(13).FontColor(Colors.BlueGrey.Darken4));
                page.Content().Border(2).BorderColor(Colors.Brown.Medium).Padding(36).Column(column =>
                {
                    column.Spacing(8);
                    column.Item().Text("CoLearnX").FontSize(12);
                    column.Item().Text(isLearningPath ? $"{stageName} Certificate" : "Course Certificate of Completion").FontSize(28).Bold();
                    column.Item().PaddingTop(8).Text("This certifies that");
                    column.Item().Text(learnerName).FontSize(24).Bold().FontColor(Colors.Brown.Darken2);
                    column.Item().Text("has completed");
                    column.Item().Text($"{courseCode}  {courseTitle}").FontSize(18).Bold();
                    if (isLearningPath) column.Item().PaddingTop(8).Text($"Learning path  {stageName}");
                    column.Item().Text($"Issued  {awardedAtUtc:yyyy-MM-dd} UTC");
                    column.Item().Text($"Verification  {verificationCode}");
                    column.Item().PaddingTop(28).Text(isLearningPath ? "Issued automatically for completed learning." : "CoLearnX course certificate.").FontSize(9);
                });
            });
        }).GeneratePdf();
    }
}
