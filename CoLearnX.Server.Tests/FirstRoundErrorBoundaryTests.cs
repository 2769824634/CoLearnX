using System.Security.Claims;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Controllers;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public sealed class FirstRoundErrorBoundaryTests
{
    private const string InternalDetail = "INTERNAL_DATABASE_DETAIL_SHOULD_NOT_REACH_THE_CLIENT";

    [Theory]
    [InlineData(false, "SAVE_FAILED")]
    [InlineData(true, "INTAKE_SAVE_FAILED")]
    public void Unknown_write_failure_hides_internal_detail_and_retains_trace(bool trainer, string expectedCode)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var http = CreateContext(services);
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var context = new ExceptionContext(action, []) { Exception = new InvalidOperationException(InternalDetail) };
        IExceptionFilter filter = trainer ? new TrainerApiErrorsAttribute() : new LaterPhaseApiErrorsAttribute();

        filter.OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(500, result.StatusCode);
        var error = Assert.IsType<ApiError>(result.Value);
        Assert.Equal(expectedCode, error.Code);
        Assert.Equal(http.TraceIdentifier, error.TraceId);
        Assert.DoesNotContain(InternalDetail, error.Message);
        Assert.Contains("could not be confirmed", error.Message);
    }

    [Fact]
    public async Task Enrollment_unknown_failure_returns_safe_message_and_trace()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var http = CreateContext(services);
        var controller = new EnrollmentsController(new FailingEnrollments(), null!, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
        var response = await controller.Enrol(new EnrolRequest(1, 1), default);
        AssertSafeBadRequest(response.Result, "ENROL_FAILED", http.TraceIdentifier);
    }

    [Fact]
    public async Task Upload_unknown_failure_returns_safe_message_and_trace()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var http = CreateContext(services);
        var controller = new MaterialsController(new FailingMaterials(), null!)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
        var response = await controller.UploadFile("Material", 1, "Testing", null, null, default);
        AssertSafeBadRequest(response.Result, "UPLOAD_FAILED", http.TraceIdentifier);
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services) => new()
    {
        RequestServices = services,
        TraceIdentifier = "first-round-error-reference",
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "Test")),
    };

    private static void AssertSafeBadRequest(IActionResult? result, string code, string trace)
    {
        var response = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiError>(response.Value);
        Assert.Equal(code, error.Code);
        Assert.Equal(trace, error.TraceId);
        Assert.DoesNotContain(InternalDetail, error.Message);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    private sealed class FailingEnrollments : IEnrollmentService
    {
        public Task<EnrolResultDto> EnrolAsync(int userId, EnrolRequest request, CancellationToken ct = default)
            => throw new InvalidOperationException(InternalDetail);
        public Task<EnrolResultDto> AcceptPostponementAsync(int userId, int enrollmentId, AcceptPostponementRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EnrollmentDto> CancelReservationAsync(int userId, int enrollmentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EnrollmentDto> WithdrawAsync(int userId, int enrollmentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<EnrollmentDto>> GetMyAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FailingMaterials : IMaterialService
    {
        public Task<MaterialDto> UploadAsync(int creatorId, int courseId, string title, string? category, string? description, IFormFile? file, CancellationToken ct = default)
            => throw new InvalidOperationException(InternalDetail);
        public Task<IReadOnlyList<MaterialDto>> ListAsync(int userId, MaterialStatus? status, int? courseId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CreatorMaterialUsageDto>> ListCreatorUsageAsync(int creatorId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MaterialFileResult> OpenDownloadAsync(int userId, int materialId, CancellationToken ct = default) => throw new NotSupportedException();
        public StorageStatusDto GetStorageStatus() => throw new NotSupportedException();
        public Task<MaterialCloudLinkDto> CreateCloudLinkAsync(int userId, int materialId, TimeSpan lifetime, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
