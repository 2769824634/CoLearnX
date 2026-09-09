using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Controllers;

[AttributeUsage(AttributeTargets.Class)]
public sealed class TrainerApiErrorsAttribute : ActionFilterAttribute, IExceptionFilter
{
    // Run before ApiController's automatic ModelState response (-2000).
    public TrainerApiErrorsAttribute() => Order = -3000;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid) return;
        var fields = context.ModelState.Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(entry => entry.Key, _ => new[] { "This field is missing, malformed or not allowed." });
        context.Result = new BadRequestObjectResult(new ApiError("INVALID_REQUEST", "Check the request fields and JSON format.", fields));
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is CourseIntakeException error)
        {
            context.Result = new ObjectResult(new ApiError(error.Code, error.Message, error.FieldErrors)) { StatusCode = error.StatusCode };
            context.ExceptionHandled = true;
        }
        else if (context.Exception is DbUpdateException)
        {
            context.HttpContext.RequestServices.GetRequiredService<ILogger<TrainerApiErrorsAttribute>>()
                .LogError(context.Exception, "Trainer Intake persistence failed");
            context.Result = new ObjectResult(new ApiError("INTAKE_SAVE_FAILED", "The Intake could not be saved. Reload before trying again.",
                new Dictionary<string, string[]>())) { StatusCode = StatusCodes.Status500InternalServerError };
            context.ExceptionHandled = true;
        }
    }
}
