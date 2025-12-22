namespace ECommerce.Api.Contracts
{
    public record ApiResponse<T>(bool Success, T Data)
    {
        public static ApiResponse<T> Ok(T data) => new(true, data);
    }
}
