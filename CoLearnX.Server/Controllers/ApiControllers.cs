using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Payments;
using CoLearnX.Server.Services;
using CoLearnX.Server.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CoLearnX.Server.Controllers;

// Register, login, me, switch-role. Keep: AuthController, route api/auth
[ApiController]
[Route("api/auth")]
public partial class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await auth.RegisterAsync(request, ct));
        }
        catch (FormatException ex)
        {
            return BadRequest(new ApiError("WEAK_PASSWORD", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiError("REGISTER_FAILED", ex.Message));
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await auth.LoginAsync(request, ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiError("LOGIN_FAILED", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError("LOGIN_FAILED", ex.Message));
        }
    }

    [HttpPost("available-roles")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AvailableRolesDto>> AvailableRoles([FromBody] AvailableRolesRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await auth.GetAvailableRolesAsync(request, ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiError("LOGIN_FAILED", ex.Message));
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserMeDto>> Me(CancellationToken ct)
    {
        var me = await auth.GetMeAsync(User.GetUserId(), User.GetActiveRole(), ct);
        return Ok(me);
    }

    [HttpPost("switch-role")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> SwitchRole([FromBody] SwitchRoleRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await auth.SwitchRoleAsync(User.GetUserId(), request, ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiError("SWITCH_ROLE_FAILED", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError("SWITCH_ROLE_FAILED", ex.Message));
        }
    }
}

// Profile read/update. Keep: UsersController, route api/users
[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(IUserService users) : ControllerBase
{
    [HttpPost("{id:int}/avatar")]
    [RequestSizeLimit(2 * 1024 * 1024 + 64 * 1024)]
    public async Task<ActionResult<UserMeDto>> UploadAvatar(int id, [FromForm] IFormFile? file, CancellationToken ct)
    {
        if (id != User.GetUserId()) return Forbid();
        try { return Ok(await users.UploadAvatarAsync(id, User.GetActiveRole(), file, ct)); }
        catch (ArgumentException ex) { return BadRequest(new ApiError("INVALID_AVATAR", ex.Message)); }
    }

    [HttpGet("{id:int}/avatar")]
    public async Task<IActionResult> Avatar(int id, CancellationToken ct)
    {
        if (id != User.GetUserId()) return Forbid();
        var file = await users.OpenAvatarAsync(id, ct);
        if (file is null) return NotFound(new ApiError("AVATAR_NOT_FOUND", "Avatar not found."));
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(file.Stream, file.ContentType);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserMeDto>> Get(int id, [FromServices] IAuthService auth, CancellationToken ct)
    {
        if (id != User.GetUserId())
            return Forbid();
        return Ok(await auth.GetMeAsync(id, User.GetActiveRole(), ct));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserMeDto>> Update(int id, [FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        if (id != User.GetUserId())
            return Forbid();
        return Ok(await users.UpdateProfileAsync(id, User.GetActiveRole(), request, ct));
    }
}

// Catalog list/detail/featured. Keep: CoursesController, route api/courses
[ApiController]
[Route("api/courses")]
public class CoursesController(ICourseService courses) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<CourseListItemDto>>> List(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] string? level,
        CancellationToken ct)
    {
        var userId = User.Identity?.IsAuthenticated == true ? User.GetUserId() : (int?)null;
        return Ok(await courses.ListAsync(userId, search, category, level, featured: null, ct));
    }

    [HttpGet("featured")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<CourseListItemDto>>> Featured(CancellationToken ct)
    {
        var userId = User.Identity?.IsAuthenticated == true ? User.GetUserId() : (int?)null;
        return Ok(await courses.ListAsync(userId, null, null, null, featured: true, ct));
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<CourseDetailDto>> Get(int id, CancellationToken ct)
    {
        var userId = User.Identity?.IsAuthenticated == true ? User.GetUserId() : (int?)null;
        var course = await courses.GetByIdAsync(id, userId, ct);
        return course is null ? NotFound(new ApiError("NOT_FOUND", "Course not found.")) : Ok(course);
    }

    [HttpPost("{id:int}/wishlist")]
    [Authorize(Roles = "Member")]
    public async Task<ActionResult<WishlistResultDto>> AddWishlist(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await courses.AddToWishlistAsync(User.GetUserId(), id, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError("NOT_FOUND", ex.Message));
        }
    }

    [HttpDelete("{id:int}/wishlist")]
    [Authorize(Roles = "Member")]
    public async Task<ActionResult<WishlistResultDto>> RemoveWishlist(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await courses.RemoveFromWishlistAsync(User.GetUserId(), id, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError("NOT_FOUND", ex.Message));
        }
    }
}

// Enrol with credits + list my enrollments. Keep: EnrollmentsController, route api/enrollments
[ApiController]
[Route("api/enrollments")]
[Authorize(Roles = "Member")]
public class EnrollmentsController(IEnrollmentService enrollments, IMemberLearningHubService hub) : ControllerBase
{
    [HttpGet("{enrollmentId:int}/materials")]
    public async Task<ActionResult<IReadOnlyList<MemberHubMaterialDto>>> Materials(int enrollmentId, CancellationToken ct)
    {
        try { return Ok(await hub.MaterialsAsync(User.GetUserId(), enrollmentId, ct)); }
        catch (KeyNotFoundException) { return NotFound(new ApiError("ENROLLMENT_NOT_FOUND", "Enrollment not found.")); }
    }

    [HttpGet("{enrollmentId:int}/recordings")]
    public async Task<ActionResult<IReadOnlyList<MemberHubRecordingDto>>> Recordings(int enrollmentId, CancellationToken ct)
    {
        try { return Ok(await hub.RecordingsAsync(User.GetUserId(), enrollmentId, ct)); }
        catch (KeyNotFoundException) { return NotFound(new ApiError("ENROLLMENT_NOT_FOUND", "Enrollment not found.")); }
    }

    [HttpGet("{enrollmentId:int}/materials/{versionId:int}/file")]
    public async Task<IActionResult> MaterialFile(int enrollmentId, int versionId, CancellationToken ct)
    {
        try
        {
            var file = await hub.OpenMaterialAsync(User.GetUserId(), enrollmentId, versionId, ct);
            return File(file.Stream, file.ContentType, file.DownloadName);
        }
        catch (KeyNotFoundException) { return NotFound(new ApiError("MATERIAL_NOT_FOUND", "Material not found.")); }
        catch (FileNotFoundException) { return NotFound(new ApiError("MATERIAL_NOT_FOUND", "Material not found.")); }
    }

    [HttpPost]
    public async Task<ActionResult<EnrolResultDto>> Enrol([FromBody] EnrolRequest request, CancellationToken ct)
    {
        try
        {
            var result = await enrollments.EnrolAsync(User.GetUserId(), request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message == "INSUFFICIENT_CREDITS")
        {
            return BadRequest(new ApiError("INSUFFICIENT_CREDITS", "Not enough credits to enrol."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError("ENROL_FAILED", ex.Message));
        }
    }

    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<EnrollmentDto>>> My(CancellationToken ct)
        => Ok(await enrollments.GetMyAsync(User.GetUserId(), ct));
}

// Packages, ledger, PayPal create/capture, top-up. Keep: CreditsController, route api/credits
[ApiController]
[Route("api/credits")]
[Authorize]
public class CreditsController(ICreditService credits, IPayPalClient payPal) : ControllerBase
{
    [HttpGet("packages")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<CreditPackageDto>>> Packages(CancellationToken ct)
        => Ok(await credits.GetPackagesAsync(ct));

    [HttpGet("paypal/config")]
    [AllowAnonymous]
    public ActionResult<PayPalClientConfigDto> PayPalConfig()
        => Ok(payPal.GetPublicConfig());

    [HttpGet("ledger/my")]
    [Authorize(Roles = "Member")]
    public async Task<ActionResult<IReadOnlyList<CreditLedgerItemDto>>> MyLedger(CancellationToken ct)
        => Ok(await credits.GetMyLedgerAsync(User.GetUserId(), ct));

    [HttpPost("paypal/create-order")]
    [Authorize(Roles = "Member")]
    public async Task<ActionResult<CreatePayPalOrderResponse>> CreatePayPalOrder(
        [FromBody] CreatePayPalOrderRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await credits.CreatePayPalOrderAsync(User.GetUserId(), request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError("PAYPAL_CREATE_FAILED", ex.Message));
        }
    }

    [HttpPost("paypal/capture")]
    [Authorize(Roles = "Member")]
    public async Task<ActionResult<CreditLedgerItemDto>> CapturePayPalOrder(
        [FromBody] CapturePayPalOrderRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await credits.CapturePayPalOrderAsync(User.GetUserId(), request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError("PAYPAL_CAPTURE_FAILED", ex.Message));
        }
    }

    [HttpPost("topup")]
    [Authorize(Roles = "Member")]
    public async Task<ActionResult<CreditLedgerItemDto>> TopUp([FromBody] TopUpRequest request, CancellationToken ct)
    {
        if (payPal.GetPublicConfig().Enabled)
            return BadRequest(new ApiError("PAYPAL_REQUIRED", "PayPal checkout is configured. Simulated top-up is disabled."));

        try
        {
            return Ok(await credits.TopUpAsync(User.GetUserId(), request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError("TOPUP_FAILED", ex.Message));
        }
    }
}

// Learning materials list/upload/download. Keep: MaterialsController, route api/materials
[ApiController]
[Route("api/materials")]
[Authorize]
public class MaterialsController(IMaterialService materials, IMaterialVersionService versions) : ControllerBase
{
    [HttpGet("usage")]
    [Authorize(Policy = CreatorAuthorization.PolicyName)]
    [LaterPhaseApiErrors]
    public async Task<ActionResult<IReadOnlyList<CreatorMaterialUsageDto>>> Usage(CancellationToken ct)
        => Ok(await materials.ListCreatorUsageAsync(User.GetUserId(), ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MaterialDto>>> List(
        [FromQuery] string? status,
        [FromQuery] int? courseId,
        CancellationToken ct)
    {
        Domain.Enums.MaterialStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Domain.Enums.MaterialStatus>(status, true, out var s))
            parsed = s;
        return Ok(await materials.ListAsync(User.GetUserId(), parsed, courseId, ct));
    }

    [HttpGet("storage")]
    public ActionResult<StorageStatusDto> Storage()
        => Ok(materials.GetStorageStatus());

    [HttpGet("{id:int}/cloud-link")]
    public async Task<ActionResult<MaterialCloudLinkDto>> CloudLink(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await materials.CreateCloudLinkAsync(User.GetUserId(), id, TimeSpan.FromDays(7), ct));
        }
        catch (FileNotFoundException)
        {
            return NotFound(new ApiError("NOT_FOUND", "Material file not found."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError("CLOUD_LINK_UNAVAILABLE", ex.Message));
        }
    }

    [HttpPost]
    [Authorize(Policy = CreatorAuthorization.PolicyName)]
    [LaterPhaseApiErrors]
    [Consumes("application/json")]
    public async Task<ActionResult<MaterialVersionDto>> SubmitVersion([FromBody] CreateMaterialVersionRequest request, CancellationToken ct)
    {
        var result = await versions.CreateAsync(User.GetUserId(), request, ct);
        return Created($"/api/materials/{result.LearningMaterialId}/versions/{result.VersionId}", result);
    }

    [HttpPost]
    [Authorize(Policy = CreatorAuthorization.PolicyName)]
    [LaterPhaseApiErrors]
    [RequestSizeLimit(MaterialFiles.MaxRequestBytes)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<MaterialDto>> UploadFile(
        [FromForm] string title,
        [FromForm] int courseId,
        [FromForm] string? category,
        [FromForm] string? description,
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        try
        {
            var created = await materials.UploadAsync(User.GetUserId(), courseId, title, category, description, file, ct);
            return Created($"/api/materials/{created.Id}", created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError("UPLOAD_FAILED", ex.Message));
        }
    }

    [HttpGet("{id:int}/file")]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        try
        {
            var file = await materials.OpenDownloadAsync(User.GetUserId(), id, ct);
            return File(file.Stream, file.ContentType, file.DownloadName);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new ApiError("NOT_FOUND", "Material file not found."));
        }
    }
}

// Member certificates. Keep: CertificatesController, route api/certificates
[ApiController]
[Route("api/certificates")]
[Authorize]
public class CertificatesController(ICertificateService certificates, ICertificateWorkflowService workflow) : ControllerBase
{
    [HttpGet("requests/my")]
    [Authorize(Roles = "Member")]
    [LaterPhaseApiErrors]
    public async Task<ActionResult<IReadOnlyList<CertificateRequestDto>>> MyRequests(CancellationToken ct)
        => Ok(await workflow.ListForMemberAsync(User.GetUserId(), ct));

    [HttpGet("eligibility")]
    [Authorize(Roles = "Member")]
    [LaterPhaseApiErrors]
    public async Task<ActionResult<IReadOnlyList<CertificateEligibilityDto>>> Eligibility(CancellationToken ct)
        => Ok(await workflow.ListEligibilityAsync(User.GetUserId(), ct));

    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<CertificateDto>>> My(CancellationToken ct)
        => Ok(await certificates.GetMyAsync(User.GetUserId(), ct));

    [HttpPost("requests")]
    [Authorize(Roles = "Member")]
    [LaterPhaseApiErrors]
    public async Task<ActionResult<CertificateRequestDto>> RequestCertificate(
        [FromBody] SubmitCertificateRequest request, CancellationToken ct)
    {
        var result = await workflow.SubmitAsync(User.GetUserId(), request, ct);
        return Created($"/api/certificates/requests/{result.Id}", result);
    }
}
