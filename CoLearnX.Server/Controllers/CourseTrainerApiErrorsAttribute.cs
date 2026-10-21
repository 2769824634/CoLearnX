using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Controllers;

[AttributeUsage(AttributeTargets.Class)]
public sealed class CourseTrainerApiErrorsAttribute : ActionFilterAttribute, IExceptionFilter
{
    public CourseTrainerApiErrorsAttribute() => Order = -3000;

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
        else if (context.Exception is DbUpdateException dbUpdate && dbUpdate.IsUniqueConstraintViolation())
        {
            context.Result = new ConflictObjectResult(new ApiError(
                "TRAINER_ALREADY_INVITED",
                "This Trainer is already invited to or teaching this Course."));
            context.ExceptionHandled = true;
        }
        else if (context.Exception is DbUpdateException)
        {
            context.HttpContext.RequestServices.GetRequiredService<ILogger<CourseTrainerApiErrorsAttribute>>()
                .LogError(context.Exception, "Course Trainer roster persistence failed");
            context.Result = new ObjectResult(new ApiError(
                "COURSE_TRAINER_SAVE_FAILED",
                "The Trainer roster could not be saved. Reload before trying again."))
            {
                StatusCode = StatusCodes.Status500InternalServerError,
            };
            context.ExceptionHandled = true;
        }
    }
}
