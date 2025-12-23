using ECommerce.Api.Contracts;
using ECommerce.Domain.Exceptions;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace ECommerce.Api.Middlewares
{
    public class ApiExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiExceptionMiddleware> _logger;

        public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeOld(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (DomainException ex)
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                context.Response.ContentType = "application/json";
                //await context.Response.WriteAsJsonAsync(new { error = ex.Message });
                await context.Response.WriteAsJsonAsync(
                    new ApiErrorResponse(
                        false,
                        new ApiError("DOMAIN_ERROR", ex.Message)
                    )
                );
            }
            catch (KeyNotFoundException ex)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Response.ContentType = "application/json";
                //await context.Response.WriteAsJsonAsync(new { error = ex.Message });
                await context.Response.WriteAsJsonAsync(
                    new ApiErrorResponse(
                        false,
                        new ApiError("INVALID_OPERATION", ex.Message)
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                //await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
                await context.Response.WriteAsJsonAsync(
                    new ApiErrorResponse(
                        false,
                        new ApiError("INVALID_OPERATION", ex.Message)
                    )
                );
            }
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ArgumentException ex)
            {
                await WriteErrorResponse(context, ex.Message, "VALIDATION_ERROR", HttpStatusCode.BadRequest);
            }

            catch (ValidationException ex)
            {
                await WriteErrorResponse(context, ex.Message, "VALIDATION_ERROR", HttpStatusCode.BadRequest);
            }
            catch (BadHttpRequestException ex) 
            {
                await WriteErrorResponse(context, ex.Message, "VALIDATION_ERROR", HttpStatusCode.BadRequest);
            }

            catch (DomainException ex)
            {
                await WriteErrorResponse(context, ex.Message, "DOMAIN_ERROR", HttpStatusCode.Conflict);
            }
            catch (InvalidOperationException ex)
            {
                await WriteErrorResponse(context, ex.Message, "INVALID_OPERATION", HttpStatusCode.BadRequest);
            }
            catch (KeyNotFoundException ex)
            {
                await WriteErrorResponse(context, ex.Message, "NOT_FOUND", HttpStatusCode.NotFound);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception");
                await WriteErrorResponse(context, "Internal server error", "INTERNAL_ERROR", HttpStatusCode.InternalServerError);
            }
        }

        private static async Task WriteErrorResponse(HttpContext context, string message, string code, HttpStatusCode statusCode)
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var errorResponse = new ApiErrorResponse(
                Success: false,
                Error: new ApiError(code, message)
            );

            await context.Response.WriteAsJsonAsync(errorResponse);
        }
    }

}
