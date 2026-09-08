using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/admin/courses")]
[Authorize(Policy = AdminAuthorization.PolicyName)]
public class AdminCoursesController(IAdminCourseReviewService courses) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminCourseDto>>> List(
        [FromQuery] CourseStatus? status,
        CancellationToken ct)
        => Ok(await courses.ListAsync(status, ct));

    [HttpPost("{courseId:int}/review")]
    public async Task<ActionResult<AdminCourseReviewResultDto>> Review(
        int courseId,
        [FromBody] AdminReviewRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await courses.ReviewAsync(
                User.GetAdminAccountId(),
                courseId,
                request,
                ct));
        }
        catch (AdminReviewValidationException ex)
        {
            return BadRequest(new ApiError(ex.Code, ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError("COURSE_NOT_FOUND", ex.Message));
        }
        catch (AdminReviewConflictException ex)
        {
            return Conflict(new ApiError(ex.Code, ex.Message));
        }
    }
}
