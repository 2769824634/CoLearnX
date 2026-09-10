using CoLearnX.Server.Data;
using CoLearnX.Server.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CoLearnX.Server.Tests;

// Isolated SQLite + Testing host.
public sealed class CoLearnXApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"colearnx-tests-{Guid.NewGuid():N}.db");
    private readonly string _uploadPath = Path.Combine(
        Path.GetTempPath(),
        $"colearnx-uploads-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            foreach (var descriptor in services.Where(d =>
                         d.ServiceType == typeof(DbContextOptions<CoLearnXDbContext>) ||
                         d.ServiceType == typeof(CoLearnXDbContext)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<CoLearnXDbContext>(options =>
                options.UseSqlite($"Data Source={_dbPath}"));

            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IFileStorage)).ToList())
                services.Remove(descriptor);
            services.AddSingleton<IFileStorage>(new LocalFileStorage(_uploadPath));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        SeedData.InitializeAsync(db).GetAwaiter().GetResult();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TryDelete(_dbPath);
        TryDelete(_dbPath + "-wal");
        TryDelete(_dbPath + "-shm");
        TryDeleteDir(_uploadPath);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private static void TryDeleteDir(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

