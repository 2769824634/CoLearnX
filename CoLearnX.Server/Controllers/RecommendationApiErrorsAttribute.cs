using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Controllers;

[AttributeUsage(AttributeTargets.Class)]
public sealed class RecommendationApiErrorsAttribute : ActionFilterAttribute, IExceptionFilter
{
    public RecommendationApiErrorsAttribute() => Order = -3000;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid) return;
        var fields = context.ModelState.Where(item => item.Value?.Errors.Count > 0)
            .ToDictionary(item => item.Key, item => item.Value!.Errors
                .Select(error => error.ErrorMessage).ToArray());
        context.Result = new BadRequestObjectResult(new ApiError("INVALID_REQUEST", "Check the request fields.", fields));
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is CourseException error)
        {
            context.Result = new ObjectResult(new ApiError(error.Code, error.Message, error.FieldErrors))
            { StatusCode = error.StatusCode };
            context.ExceptionHandled = true;
        }
        else if (context.Exception is DbUpdateException)
        {
            context.HttpContext.RequestServices.GetRequiredService<ILogger<RecommendationApiErrorsAttribute>>()
                .LogError(context.Exception, "Recommendation persistence failed");
            context.Result = new ObjectResult(new ApiError("RECOMMENDATION_SAVE_FAILED", "The change could not be saved."))
            { StatusCode = StatusCodes.Status500InternalServerError };
            context.ExceptionHandled = true;
        }
    }
}
