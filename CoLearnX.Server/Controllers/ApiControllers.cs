using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Payments;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

// Register, login, me, switch-role. Keep: AuthController, route api/auth
[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await auth.RegisterAsync(request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiError("REGISTER_FAILED", ex.Message));
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
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
    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserMeDto>> Get(int id, [FromServices] IAuthService auth, CancellationToken ct)
    {
        if (id != User.GetUserId() && !User.IsInRole("Admin"))
            return Forbid();
        return Ok(await auth.GetMeAsync(id, User.GetActiveRole(), ct));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserMeDto>> Update(int id, [FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        if (id != User.GetUserId() && !User.IsInRole("Admin"))
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
        catch (KeyNotFoundException)
        {
            return NotFound(new ApiError("NOT_FOUND", "Course not found."));
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
        catch (KeyNotFoundException)
        {
            return NotFound(new ApiError("NOT_FOUND", "Course not found."));
        }
    }

    [HttpPost]
    [Authorize(Roles = "Trainer,Admin")]
    public ActionResult Create()
    {
        // Framework placeholder — full create wizard in later iteration
        return StatusCode(StatusCodes.Status501NotImplemented, new ApiError("NOT_IMPLEMENTED", "Course creation wizard coming next."));
    }
}

// Enrol with credits + list my enrollments. Keep: EnrollmentsController, route api/enrollments
[ApiController]
[Route("api/enrollments")]
[Authorize(Roles = "Member")]
public class EnrollmentsController(IEnrollmentService enrollments) : ControllerBase
{
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

// Learning materials list/upload stub. Keep: MaterialsController, route api/materials
[ApiController]
[Route("api/materials")]
[Authorize]
public class MaterialsController(IMaterialService materials) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MaterialDto>>> List([FromQuery] string? status, CancellationToken ct)
    {
        Domain.Enums.MaterialStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Domain.Enums.MaterialStatus>(status, true, out var s))
            parsed = s;
        return Ok(await materials.ListAsync(parsed, ct));
    }

    [HttpPost]
    [Authorize(Roles = "Creator,Admin")]
    public ActionResult Upload()
        => StatusCode(StatusCodes.Status501NotImplemented, new ApiError("NOT_IMPLEMENTED", "Material upload coming next."));
}

// Member certificates. Keep: CertificatesController, route api/certificates
[ApiController]
[Route("api/certificates")]
[Authorize]
public class CertificatesController(ICertificateService certificates) : ControllerBase
{
    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<CertificateDto>>> My(CancellationToken ct)
        => Ok(await certificates.GetMyAsync(User.GetUserId(), ct));
}

// Admin ledger + review stubs. Keep: AdminController, route api/admin
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(IAdminService admin) : ControllerBase
{
    [HttpGet("credits/ledger")]
    public async Task<ActionResult<IReadOnlyList<CreditLedgerItemDto>>> Ledger(CancellationToken ct)
        => Ok(await admin.GetLedgerAsync(ct));

    [HttpPut("materials/{id:int}/review")]
    public ActionResult ReviewMaterial(int id)
        => StatusCode(StatusCodes.Status501NotImplemented, new ApiError("NOT_IMPLEMENTED", $"Material {id} review coming next."));
}
