using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace PropNest.Api.Extensions
{
    public static class ControllerExtensions
    {
        public static ObjectResult HandleException(this ControllerBase controller, Exception exception)
        {
            return exception switch
            {
                KeyNotFoundException => controller.Problem(
                    detail: exception.Message,
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found"),
                UnauthorizedAccessException => controller.Problem(
                    detail: exception.Message,
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden"),
                ArgumentException => controller.Problem(
                    detail: exception.Message,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Bad Request"),
                DbUpdateConcurrencyException => controller.Problem(
                    detail: "The listing has been modified by another request. Please refresh and try again.",
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Concurrency Conflict"),
                InvalidOperationException => controller.Problem(
                    detail: exception.Message,
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict"),
                _ => controller.Problem(
                    detail: "An unexpected error occurred.",
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error")
            };
        }
    }
}
