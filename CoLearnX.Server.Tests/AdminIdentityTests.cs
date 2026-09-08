using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace CoLearnX.Server.Tests;

public class AdminIdentityTests
{
    private static readonly JwtOptions TestJwtOptions = new()
    {
        Issuer = "CoLearnX.Tests",
        Audience = "CoLearnX.Tests.Client",
        SigningKey = "CoLearnX-D1a-Tests-Signing-Key-At-Least-32-Characters",
        ExpiryMinutes = 15,
    };

    [Fact]
    public async Task AdminLogin_IssuesAdminOnlyToken_AndWritesAuditLog()
    {
        await using var harness = await TestDatabase.CreateAsync();
        var admin = await harness.AddAdminAsync("admin@colearnx.test", "Password123!");
        var service = CreateAdminAuthService(harness.Db);

        var response = await service.LoginAsync(new AdminLoginRequest(admin.Email, "Password123!"));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);
        Assert.Equal(AuthTokenSubjects.Admin, jwt.Claims.Single(c => c.Type == AuthTokenSubjects.ClaimType).Value);
        Assert.Equal(admin.Id.ToString(), jwt.Claims.Single(c => c.Type == AdminAuthorization.AdminAccountIdClaim).Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == ClaimTypes.Role);

        var auditLog = await harness.Db.AuditLogs.SingleAsync();
        Assert.Equal(admin.Id, auditLog.AdminAccountId);
        Assert.Null(auditLog.UserId);
        Assert.Equal("AdminLogin", auditLog.Action);
        Assert.Equal("Succeeded", auditLog.Result);
    }

    [Fact]
    public async Task AdminLogin_DoesNotAuthenticateAnOrdinaryUserWithTheSameCredentials()
    {
        await using var harness = await TestDatabase.CreateAsync();
        harness.Db.Users.Add(new User
        {
            Email = "user@colearnx.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            FullName = "Ordinary User",
            DisplayName = "User",
        });
        await harness.Db.SaveChangesAsync();
        var service = CreateAdminAuthService(harness.Db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(new AdminLoginRequest("user@colearnx.test", "Password123!")));
    }

    [Fact]
    public async Task AdminLogin_RejectsInactiveAdminAccount()
    {
        await using var harness = await TestDatabase.CreateAsync();
        var admin = await harness.AddAdminAsync("inactive@colearnx.test", "Password123!", isActive: false);
        var service = CreateAdminAuthService(harness.Db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(new AdminLoginRequest(admin.Email, "Password123!")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuditLog_RequiresExactlyOneActor(bool assignBothActors)
    {
        await using var harness = await TestDatabase.CreateAsync();
        var admin = await harness.AddAdminAsync("admin@colearnx.test", "Password123!");
        var user = new User
        {
            Email = "user@colearnx.test",
            PasswordHash = "not-used",
            FullName = "Ordinary User",
            DisplayName = "User",
        };
        harness.Db.Users.Add(user);
        await harness.Db.SaveChangesAsync();

        harness.Db.AuditLogs.Add(new AuditLog
        {
            AdminAccountId = assignBothActors ? admin.Id : null,
            UserId = assignBothActors ? user.Id : null,
            Action = "ConstraintTest",
            EntityType = "Test",
            Result = "Rejected",
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => harness.Db.SaveChangesAsync());
    }

    private static AdminAuthService CreateAdminAuthService(CoLearnXDbContext db)
    {
        var tokens = new AdminTokenService(Options.Create(TestJwtOptions));
        var auditLogs = new AuditLogService(db);
        return new AdminAuthService(db, tokens, auditLogs);
    }

    private sealed class TestDatabase(SqliteConnection connection, CoLearnXDbContext db) : IAsyncDisposable
    {
        public CoLearnXDbContext Db { get; } = db;

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<CoLearnXDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new CoLearnXDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, db);
        }

        public async Task<AdminAccount> AddAdminAsync(string email, string password, bool isActive = true)
        {
            var admin = new AdminAccount
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsActive = isActive,
            };
            Db.AdminAccounts.Add(admin);
            await Db.SaveChangesAsync();
            return admin;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
