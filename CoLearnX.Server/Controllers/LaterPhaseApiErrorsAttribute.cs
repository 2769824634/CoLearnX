using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Controllers;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class LaterPhaseApiErrorsAttribute : ActionFilterAttribute, IExceptionFilter
{
    public LaterPhaseApiErrorsAttribute() => Order = -3000;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid) return;
        var fields = context.ModelState.Where(item => item.Value?.Errors.Count > 0)
            .ToDictionary(item => item.Key, item => item.Value!.Errors
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "This field is invalid." : error.ErrorMessage).ToArray());
        context.Result = new BadRequestObjectResult(new ApiError("INVALID_REQUEST", "Check the request fields and JSON format.", fields));
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is LaterPhaseException error)
        {
            context.Result = new ObjectResult(new ApiError(error.Code, error.Message, error.FieldErrors))
            {
                StatusCode = error.StatusCode,
            };
            context.ExceptionHandled = true;
            return;
        }
        if (context.Exception is not OperationCanceledException)
        {
            context.HttpContext.RequestServices.GetRequiredService<ILogger<LaterPhaseApiErrorsAttribute>>()
                .LogError(context.Exception, "Later Phase operation failed; traceId={TraceId}", context.HttpContext.TraceIdentifier);
            context.Result = new ObjectResult(new ApiError("SAVE_FAILED", "The result could not be confirmed. Check current records before trying again.",
                TraceId: context.HttpContext.TraceIdentifier))
            {
                StatusCode = StatusCodes.Status500InternalServerError,
            };
            context.ExceptionHandled = true;
        }
    }
}
