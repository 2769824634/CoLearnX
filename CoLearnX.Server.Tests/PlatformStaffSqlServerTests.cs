using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Services;
using CoLearnX.Server.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public sealed class PlatformStaffSqlServerTests
{
    [Fact]
    public async Task Provision_Succeeds_WhenSqlServerRetryOnFailureIsEnabled()
    {
        var database = $"colearnx-staff-{Guid.NewGuid():N}";
        var connection =
            $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5";
        var options = new DbContextOptionsBuilder<CoLearnXDbContext>()
            .UseSqlServer(connection, sql => sql.EnableRetryOnFailure())
            .Options;
        await using var db = new CoLearnXDbContext(options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            var admin = new AdminAccount
            {
                Email = $"admin.{Guid.NewGuid():N}@colearnx.test",
                PasswordHash = "hash",
            };
            db.AdminAccounts.Add(admin);
            await db.SaveChangesAsync();

            var service = new AdminStaffService(db, new RoleRequestFileStorage(new MemoryFileStorage()));
            await using var stream = new MemoryStream("staff-document"u8.ToArray());
            var result = await service.ProvisionAsync(admin.Id, new ProvisionStaffRequest
            {
                Email = $"trainer.{Guid.NewGuid():N}@example.com",
                FullName = "Trainer One",
                Phone = "0400000001",
                Document = new FormFile(stream, 0, stream.Length, "document", "trainer.pdf"),
            });

            Assert.Equal(1, result.Staff.StaffNumber);
            Assert.False(string.IsNullOrWhiteSpace(result.InitialPassword));
            Assert.Equal(1, await db.StaffAccounts.CountAsync());
            Assert.Equal(1, await db.Users.CountAsync(user => user.Email == "staff-1@staff.colearnx"));
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed class MemoryFileStorage : IFileStorage
    {
        public string Provider => "Memory";
        public bool CanIssueCloudLinks => false;
        public string? Container => "role-requests";
        public Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<Stream?> OpenAsync(string key, CancellationToken ct = default)
            => Task.FromResult<Stream?>(null);
        public Task<Uri?> TryCreateReadUriAsync(string key, TimeSpan lifetime, CancellationToken ct = default)
            => Task.FromResult<Uri?>(null);
        public Task DeleteAsync(string key, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
