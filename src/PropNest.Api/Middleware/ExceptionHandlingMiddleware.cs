using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace PropNest.Api.Middleware;

public sealed partial class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for request {RequestPath}.")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string requestPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Response has already started for {RequestPath}. Cannot write ProblemDetails.")]
    private static partial void LogResponseAlreadyStarted(ILogger logger, string requestPath);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                LogResponseAlreadyStarted(logger, context.Request.Path);
                throw;
            }

            LogUnhandledException(logger, exception, context.Request.Path);
            var problem = CreateProblemDetails(context, exception);
            context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(problem);
        }
    }

    private static ProblemDetails CreateProblemDetails(HttpContext context, Exception exception)
    {
        var status = exception switch
        {
            KeyNotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            ArgumentException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        return new ProblemDetails
        {
            Status = status,
            Title = status switch
            {
                StatusCodes.Status404NotFound => "Not Found",
                StatusCodes.Status403Forbidden => "Forbidden",
                StatusCodes.Status409Conflict => "Conflict",
                StatusCodes.Status400BadRequest => "Bad Request",
                _ => "An unexpected error occurred."
            },
            Detail = status == StatusCodes.Status500InternalServerError
                ? "An internal server error occurred."
                : exception.Message,
            Instance = context.Request.Path
        };
    }
}
