using System.Net.Mail;
using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CoLearnX.Server.Tests;

public class FirstRoundSecurityMailTests
{
    [Fact]
    public async Task Production_rejects_the_shared_demo_admin_password()
    {
        using var factory = new CoLearnXApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var service = new AdminAuthService(db, scope.ServiceProvider.GetRequiredService<IAdminTokenService>(),
            scope.ServiceProvider.GetRequiredService<IAuditLogService>(), new ProductionEnvironment());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(new AdminLoginRequest(SeedData.AdminEmail, SeedData.DemoPassword)));
    }

    [Fact]
    public async Task Production_seed_disables_shared_admin_but_preserves_rotated_password()
    {
        using var factory = new CoLearnXApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var admin = await db.AdminAccounts.SingleAsync(x => x.Email == SeedData.AdminEmail);
        await SeedData.InitializeAsync(db, seedDemoAdmin: false);
        await db.Entry(admin).ReloadAsync();
        Assert.False(admin.IsActive);
        admin.IsActive = true;
        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("PrivatelyRotated784!");
        await db.SaveChangesAsync();
        await SeedData.InitializeAsync(db, seedDemoAdmin: false);
        await db.Entry(admin).ReloadAsync();
        Assert.True(admin.IsActive);
        Assert.True(BCrypt.Net.BCrypt.Verify("PrivatelyRotated784!", admin.PasswordHash));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Account_eml_has_plain_text_first_and_html_last(bool reset)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"clx-mail-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var link = "https://colearnx.xyz/" + (reset ? "reset-password" : "verify-email") + "#token=local-test";
            var composed = reset ? AccountMail.PasswordReset(link) : AccountMail.Verification(link);
            using var message = AccountMail.CreateMessage("sender@example.com", "recipient@example.com", composed);
            using var smtp = new SmtpClient { DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                PickupDirectoryLocation = directory };
            await smtp.SendMailAsync(message);
            var eml = await File.ReadAllTextAsync(Directory.GetFiles(directory).Single());
            Assert.Contains("multipart/alternative", eml, StringComparison.OrdinalIgnoreCase);
            var plain = eml.IndexOf("Content-Type: text/plain", StringComparison.OrdinalIgnoreCase);
            var html = eml.IndexOf("Content-Type: text/html", StringComparison.OrdinalIgnoreCase);
            Assert.True(plain >= 0 && html > plain, "The readable HTML alternative should follow the plain-text fallback.");
            Assert.Contains("charset=utf-8", eml, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2, eml.Split("Content-Type: text/", StringSplitOptions.None).Length - 1);
            var bodies = MailTestBodies.Decode(eml);
            Assert.Contains(link, bodies);
            Assert.Contains("<!DOCTYPE html>", bodies);
            Assert.Contains("CoLearnX Team", bodies);
        }
        finally
        {
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
