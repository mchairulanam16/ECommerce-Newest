namespace ECommerce.Api.Contracts
{
    public record ApiErrorResponse(bool Success, ApiError Error);

    public record ApiError(string Code, string Message);
}
