namespace ECommerce.Api.Contracts
{
    public record ApiResponse<T>(bool Success, T Data, string? Message = null)
    {
        public static ApiResponse<T> Ok(T data, string? message = null)
            => new(true, data, message);

        public static ApiResponse<T> Created(T data, string? message = "Resource created successfully")
            => new(true, data, message);
    }
}
