namespace PaymentCartService.Exceptions;

public class CartExpiredException : Exception
{
    public string? ErrorCode { get; }
    public CartExpiredException(string message, string? errorCode = "CART-4091") : base(message)
        => ErrorCode = errorCode;
}
