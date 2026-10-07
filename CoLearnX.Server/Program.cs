using System.IO.Compression;
using System.Text;
using System.Threading.RateLimiting;
using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Payments;
using CoLearnX.Server.Services;
using CoLearnX.Server.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// JWT + PayPal options (secrets via user-secrets / env, not source)
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<PayPalOptions>(builder.Configuration.GetSection(PayPalOptions.SectionName));
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

// Shared DbContext name: CoLearnXDbContext
var configuredConnection = builder.Configuration.GetConnectionString("Default") ?? "Data Source=colearnx-bd.db";
var useSqlServer = DatabaseEngine.IsSqlServer(configuredConnection);
string databaseDescription;
if (useSqlServer)
{
    var sqlConnection = DatabaseEngine.WithSqlServerDefaults(configuredConnection);
    databaseDescription = "Azure SQL / SQL Server";
    builder.Services.AddDbContext<CoLearnXDbContext>(options =>
        options.UseSqlServer(sqlConnection, sql => sql.EnableRetryOnFailure()));
}
else
{
    var sqliteConnection = new SqliteConnectionStringBuilder(configuredConnection);
    sqliteConnection.DataSource = ResolveSqliteDataSource(builder, sqliteConnection.DataSource);
    databaseDescription = $"SQLite {sqliteConnection.DataSource}";
    builder.Services.AddDbContext<CoLearnXDbContext>(options =>
        options.UseSqlite(sqliteConnection.ConnectionString));
}

builder.Services.AddHttpClient("PayPal");
builder.Services.AddSingleton<IPayPalClient, PayPalClient>();

// Keep these interface + impl pairs when extending services
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAdminTokenService, AdminTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<PasswordResetOptions>(builder.Configuration.GetSection("PasswordReset"));
builder.Services.AddScoped<PasswordResetService>();
builder.Services.AddScoped<EmailVerificationService>();
builder.Services.AddSingleton<PasswordResetMailSender>();
builder.Services.AddSingleton<IPasswordResetMailSender>(sp => sp.GetRequiredService<PasswordResetMailSender>());
builder.Services.AddSingleton<IEmailVerificationMailSender>(sp => sp.GetRequiredService<PasswordResetMailSender>());
builder.Services.AddSingleton<IBusinessNotificationMailSender>(sp => sp.GetRequiredService<PasswordResetMailSender>());
builder.Services.AddScoped<NotificationEmailDispatcher>();
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAdminRoleRequestService, AdminRoleRequestService>();
builder.Services.AddScoped<IRoleRequestService, RoleRequestService>();
builder.Services.AddScoped<IAdminCourseReviewService, AdminCourseReviewService>();
builder.Services.AddScoped<IAuthorizationHandler, ActiveAdminAccountHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, TrainerAuthorizationResultHandler>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ICourseIntakeService, CourseIntakeService>();
builder.Services.AddScoped<ITrainerDeliveryService, TrainerDeliveryService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IIntakeSettlementService, IntakeSettlementService>();
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddHostedService<IntakeSettlementWorker>();
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddScoped<IMemberLearningHubService, MemberLearningHubService>();
builder.Services.AddScoped<ICreditService, CreditService>();
builder.Services.AddScoped<ICertificateService, CertificateService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<IMaterialService, MaterialService>();
builder.Services.AddScoped<IMaterialVersionService, MaterialVersionService>();
builder.Services.AddScoped<ITrainerLaterPhaseService, TrainerLaterPhaseService>();
builder.Services.AddScoped<ICertificateWorkflowService, CertificateWorkflowService>();
builder.Services.AddScoped<IAdminFinanceService, AdminFinanceService>();
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = Math.Max(
    MaterialFiles.MaxRequestBytes,
    RoleRequestFiles.MaxRequestBytes));
builder.Services.AddSingleton<IFileStorage>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    return CreateFileStorage(opts, env, opts.Container, localSubfolder: null);
});
builder.Services.AddSingleton<IRoleRequestFileStorage>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var container = string.IsNullOrWhiteSpace(opts.RoleRequestsContainer) ? "role-requests" : opts.RoleRequestsContainer;
    return new RoleRequestFileStorage(CreateFileStorage(opts, env, container, "role-requests"));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = CreateTokenValidationParameters(jwt);
        options.Events = ExclusiveSessionEvents(AuthTokenSubjects.User);
    })
    .AddJwtBearer(AdminAuthorization.SchemeName, options =>
    {
        options.TokenValidationParameters = CreateTokenValidationParameters(jwt);
        options.Events = ExclusiveSessionEvents(AuthTokenSubjects.Admin);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ActiveMember", policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(AuthTokenSubjects.ClaimType, AuthTokenSubjects.User);
        policy.RequireClaim("active_role", "Member");
        policy.RequireRole("Member");
    });
    options.AddPolicy(TrainerAuthorization.PolicyName, policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(AuthTokenSubjects.ClaimType, AuthTokenSubjects.User);
        policy.RequireClaim("active_role", "Trainer");
        policy.RequireRole("Trainer");
    });
    options.AddPolicy(CreatorAuthorization.PolicyName, policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(AuthTokenSubjects.ClaimType, AuthTokenSubjects.User);
        policy.RequireClaim("active_role", "Creator");
        policy.RequireRole("Creator");
    });
    options.AddPolicy(AdminAuthorization.PolicyName, policy =>
    {
        policy.AddAuthenticationSchemes(AdminAuthorization.SchemeName);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(AuthTokenSubjects.ClaimType, AuthTokenSubjects.Admin);
        policy.RequireClaim(AdminAuthorization.AdminAccountIdClaim);
        policy.AddRequirements(ActiveAdminAccountRequirement.Instance);
    });
});
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevClient", policy =>
        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials()
            .SetIsOriginAllowed(_ => true));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        if (context.HttpContext.Request.Path == "/api/auth/forgot-password")
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
            await context.HttpContext.Response.WriteAsJsonAsync(new PasswordResetResponse(PasswordResetService.RequestMessage), token);
            return;
        }
        context.HttpContext.Response.Headers.RetryAfter = "600";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ApiError("TOO_MANY_REQUESTS", "Too many sign-in attempts. Try again in a few minutes."),
            token);
    };
    var testing = builder.Environment.IsEnvironment("Testing");
    options.AddPolicy("password-reset", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitClientKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitClientKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = testing ? 1000 : 20,
                Window = testing ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseRateLimiter();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
    await SeedData.InitializeAsync(db, seedDemoAdmin: !app.Environment.IsProduction());
}

var storage = app.Services.GetRequiredService<IFileStorage>();
var roleRequestStorage = app.Services.GetRequiredService<IRoleRequestFileStorage>();
app.Logger.LogInformation(
    "File storage: {Provider} container={Container} cloudLinks={CloudLinks}",
    storage.Provider,
    storage.Container ?? "(local disk)",
    storage.CanIssueCloudLinks);
app.Logger.LogInformation(
    "Role-request storage: {Provider} container={Container}",
    roleRequestStorage.Provider,
    roleRequestStorage.Container ?? "(local disk)");
app.Logger.LogInformation("Database: {Database}", databaseDescription);
var passwordReset = app.Configuration.GetSection("PasswordReset").Get<PasswordResetOptions>() ?? new PasswordResetOptions();
var passwordResetReady = !string.IsNullOrWhiteSpace(passwordReset.SmtpHost)
    && !string.IsNullOrWhiteSpace(passwordReset.SmtpUsername)
    && !string.IsNullOrWhiteSpace(passwordReset.SmtpPassword)
    && !string.IsNullOrWhiteSpace(passwordReset.FromAddress)
    && !passwordReset.FromAddress.EndsWith(".test", StringComparison.OrdinalIgnoreCase);
if (app.Environment.IsProduction() || string.Equals(passwordReset.DeliveryMode, "Smtp", StringComparison.OrdinalIgnoreCase))
{
    if (passwordResetReady)
        app.Logger.LogInformation(
            "Password reset mail: Smtp host={Host} from={From} origin={Origin}",
            passwordReset.SmtpHost,
            passwordReset.FromAddress,
            string.IsNullOrWhiteSpace(passwordReset.ClientBaseUrl) ? "(request host)" : passwordReset.ClientBaseUrl);
    else
        app.Logger.LogWarning(
            "Password reset mail is not configured. Set PasswordReset__SmtpHost, PasswordReset__FromAddress, PasswordReset__SmtpUsername and PasswordReset__SmtpPassword on the App Service.");
}
if (!useSqlServer
    && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")))
{
    app.Logger.LogWarning(
        "App Service is still using SQLite. Set ConnectionStrings__Default to the Azure SQL ADO.NET connection string.");
}

if (app.Environment.IsProduction()
    && jwt.SigningKey.Contains("Change-In-Production", StringComparison.OrdinalIgnoreCase))
{
    app.Logger.LogWarning("Jwt:SigningKey is still the development default. Set Jwt__SigningKey on the App Service.");
}
var payPalOpts = app.Configuration.GetSection(PayPalOptions.SectionName).Get<PayPalOptions>() ?? new PayPalOptions();
if (payPalOpts.IsConfigured)
    app.Logger.LogInformation("PayPal: {Mode} currency={Currency} clientIdLength={Length}", payPalOpts.Mode, payPalOpts.Currency, payPalOpts.ClientId.Trim().Length);
else
    app.Logger.LogWarning("PayPal is not configured. Set PayPal__ClientId and PayPal__ClientSecret on the App Service to the sandbox REST app credentials, not the setting names.");

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapStaticAssets();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("DevClient");
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapControllers();
app.MapFallbackToFile("/index.html");

app.Run();

static string RateLimitClientKey(HttpContext httpContext)
{
    var forwarded = httpContext.Request.Headers["X-Forwarded-For"].ToString();
    if (!string.IsNullOrWhiteSpace(forwarded))
    {
        var first = forwarded.Split(',')[0].Trim();
        if (first.Length > 0)
            return first;
    }

    return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

static string ResolveSqliteDataSource(WebApplicationBuilder builder, string dataSource)
{
    if (string.IsNullOrWhiteSpace(dataSource)
        || string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
        || dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        return dataSource;

    if (Path.IsPathRooted(dataSource))
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dataSource)!);
        return dataSource;
    }

    var home = Environment.GetEnvironmentVariable("HOME");
    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"))
        && !string.IsNullOrWhiteSpace(home))
    {
        var dataDir = Path.Combine(home, "data");
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, Path.GetFileName(dataSource));
    }

    return Path.Combine(builder.Environment.ContentRootPath, dataSource);
}

static TokenValidationParameters CreateTokenValidationParameters(JwtOptions options)
    => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = options.Issuer,
        ValidAudience = options.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        ClockSkew = TimeSpan.Zero,
    };

static JwtBearerEvents ExclusiveSessionEvents(string expectedSubjectType)
    => new()
    {
        OnTokenValidated = context => SessionStampValidator.ValidateAsync(context, expectedSubjectType),
    };

static IFileStorage CreateFileStorage(
    StorageOptions opts,
    IWebHostEnvironment env,
    string? container,
    string? localSubfolder)
{
    if (opts.UseAzure)
        return new AzureBlobFileStorage(opts.ConnectionString, container);

    var root = string.IsNullOrWhiteSpace(opts.RootPath)
        ? Path.Combine(env.ContentRootPath, "App_Data", "uploads")
        : opts.RootPath;
    if (!string.IsNullOrWhiteSpace(localSubfolder))
        root = Path.Combine(root, localSubfolder);
    return new LocalFileStorage(root);
}

public partial class Program;
