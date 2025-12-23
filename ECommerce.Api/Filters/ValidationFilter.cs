using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ECommerce.Api.Contracts;
using System.Net;

namespace ECommerce.Api.Filters
{
    public class ValidationFilter : IAsyncResultFilter
    {
        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            // Only modify validation error responses
            if (context.Result is BadRequestObjectResult badRequestResult)
            {
                string errorMessage = "Validation failed";
                string errorCode = "VALIDATION_ERROR";

                // Handle ValidationProblemDetails (ASP.NET Core's default for model validation)
                if (badRequestResult.Value is ValidationProblemDetails validationDetails)
                {
                    // Extract the first error message
                    errorMessage = validationDetails.Errors
                        .SelectMany(e => e.Value)
                        .FirstOrDefault() ?? errorMessage;

                    // Format to ApiErrorResponse
                    var errorResponse = new ApiErrorResponse(
                        Success: false,
                        Error: new ApiError(errorCode, errorMessage)
                    );

                    badRequestResult.Value = errorResponse;
                }
                // Handle ProblemDetails
                else if (badRequestResult.Value is ProblemDetails problemDetails)
                {
                    errorMessage = problemDetails.Title ?? errorMessage;
                    var errorResponse = new ApiErrorResponse(
                        Success: false,
                        Error: new ApiError(errorCode, errorMessage)
                    );

                    badRequestResult.Value = errorResponse;
                }
                // Handle string responses
                else if (badRequestResult.Value is string stringValue)
                {
                    var errorResponse = new ApiErrorResponse(
                        Success: false,
                        Error: new ApiError(errorCode, stringValue)
                    );

                    badRequestResult.Value = errorResponse;
                }
            }

            await next();
        }
    }
}