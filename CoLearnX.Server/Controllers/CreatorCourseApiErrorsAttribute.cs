using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Controllers;

[AttributeUsage(AttributeTargets.Class)]
public sealed class CreatorCourseApiErrorsAttribute : ActionFilterAttribute, IExceptionFilter
{
    public CreatorCourseApiErrorsAttribute() => Order = -3000;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid) return;
        var fields = context.ModelState.Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(entry => entry.Key, entry => entry.Value!.Errors
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "This field is invalid."
                    : error.ErrorMessage).ToArray());
        context.Result = new BadRequestObjectResult(
            new ApiError("INVALID_REQUEST", "Check the request fields and JSON format.", fields));
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is CourseException error)
        {
            context.Result = new ObjectResult(new ApiError(error.Code, error.Message, error.FieldErrors))
            {
                StatusCode = error.StatusCode,
            };
            context.ExceptionHandled = true;
        }
        else if (context.Exception is DbUpdateException
            {
                InnerException: SqliteException { SqliteExtendedErrorCode: 2067 }
            })
        {
            context.Result = new ConflictObjectResult(new ApiError(
                "COURSE_CODE_EXISTS",
                "Course code is already in use.",
                new Dictionary<string, string[]> { ["code"] = ["Course code is already in use."] }));
            context.ExceptionHandled = true;
        }
        else if (context.Exception is DbUpdateException)
        {
            context.HttpContext.RequestServices.GetRequiredService<ILogger<CreatorCourseApiErrorsAttribute>>()
                .LogError(context.Exception, "Creator Course persistence failed");
            context.Result = new ObjectResult(new ApiError(
                "COURSE_SAVE_FAILED",
                "The Course could not be saved. Reload before trying again."))
            {
                StatusCode = StatusCodes.Status500InternalServerError,
            };
            context.ExceptionHandled = true;
        }
    }
}
