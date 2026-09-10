using System.Text;
using CoLearnX.Server.Auth;
using CoLearnX.Server.Data;
using CoLearnX.Server.Payments;
using CoLearnX.Server.Services;
using CoLearnX.Server.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// JWT + PayPal options (secrets via user-secrets / env, not source)
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<PayPalOptions>(builder.Configuration.GetSection(PayPalOptions.SectionName));
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

// Shared DbContext name: CoLearnXDbContext
var configuredConnection = builder.Configuration.GetConnectionString("Default") ?? "Data Source=colearnx-bd.db";
var sqliteConnection = new SqliteConnectionStringBuilder(configuredConnection);
if (!string.IsNullOrWhiteSpace(sqliteConnection.DataSource)
    && !string.Equals(sqliteConnection.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
    && !sqliteConnection.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
    && !Path.IsPathRooted(sqliteConnection.DataSource))
    sqliteConnection.DataSource = Path.Combine(builder.Environment.ContentRootPath, sqliteConnection.DataSource);

builder.Services.AddDbContext<CoLearnXDbContext>(options =>
    options.UseSqlite(sqliteConnection.ConnectionString));

builder.Services.AddHttpClient("PayPal");
builder.Services.AddSingleton<IPayPalClient, PayPalClient>();

// Keep these interface + impl pairs when extending services
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAdminTokenService, AdminTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAdminRoleRequestService, AdminRoleRequestService>();
builder.Services.AddScoped<IAdminCourseReviewService, AdminCourseReviewService>();
builder.Services.AddScoped<IAuthorizationHandler, ActiveAdminAccountHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, TrainerAuthorizationResultHandler>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ICourseIntakeService, CourseIntakeService>();
builder.Services.AddScoped<ITrainerDeliveryService, TrainerDeliveryService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<ICreditService, CreditService>();
builder.Services.AddScoped<ICertificateService, CertificateService>();
builder.Services.AddScoped<IMaterialService, MaterialService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IMaterialVersionService, MaterialVersionService>();
builder.Services.AddScoped<ITrainerLaterPhaseService, TrainerLaterPhaseService>();
builder.Services.AddScoped<ICertificateWorkflowService, CertificateWorkflowService>();
builder.Services.AddScoped<IAdminFinanceService, AdminFinanceService>();
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = MaterialFiles.MaxRequestBytes);
builder.Services.AddSingleton<IFileStorage>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
    if (opts.UseAzure)
        return new AzureBlobFileStorage(sp.GetRequiredService<IOptions<StorageOptions>>());

    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var root = string.IsNullOrWhiteSpace(opts.RootPath)
        ? Path.Combine(env.ContentRootPath, "App_Data", "uploads")
        : opts.RootPath;
    return new LocalFileStorage(root);
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = CreateTokenValidationParameters(jwt);
        options.Events = RequireSubjectType(AuthTokenSubjects.User);
    })
    .AddJwtBearer(AdminAuthorization.SchemeName, options =>
    {
        options.TokenValidationParameters = CreateTokenValidationParameters(jwt);
        options.Events = RequireSubjectType(AuthTokenSubjects.Admin);
    });

builder.Services.AddAuthorization(options =>
{
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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
    await SeedData.InitializeAsync(db);
}

var storage = app.Services.GetRequiredService<IFileStorage>();
app.Logger.LogInformation(
    "File storage: {Provider} container={Container} cloudLinks={CloudLinks}",
    storage.Provider,
    storage.Container ?? "(local disk)",
    storage.CanIssueCloudLinks);

app.UseDefaultFiles();
app.MapStaticAssets();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("DevClient");
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("/index.html");

app.Run();

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

static JwtBearerEvents RequireSubjectType(string expectedSubjectType)
    => new()
    {
        OnTokenValidated = context =>
        {
            var actualSubjectType = context.Principal?.FindFirst(AuthTokenSubjects.ClaimType)?.Value;
            if (!string.Equals(actualSubjectType, expectedSubjectType, StringComparison.Ordinal))
                context.Fail("Token subject type is not valid for this authentication scheme.");

            return Task.CompletedTask;
        },
    };

public partial class Program;
