namespace SalesManagementService.Infrastructure.Security;

public static class ErrorCodes
{
    // 400 Bad Request
    public const string InvalidRequest = "ORD-4001";
    public const string InvalidDateRange = "ORD-4002";
    public const string InvalidOrderStatusTransition = "ORD-4003";

    // 404 Not Found
    public const string OrderNotFound = "ORD-4041";
    public const string OrderItemNotFound = "ORD-4042";
    public const string ShipmentNotFound = "ORD-4043";
    public const string ReturnNotFound = "ORD-4044";
    public const string InvoiceNotFound = "ORD-4045";

    // 409 Conflict
    public const string ConcurrencyConflict = "ORD-4091";
    public const string IdempotencyConflict = "ORD-4092";

    // 422 Unprocessable Entity
    public const string InsufficientStock = "ORD-4221";
    public const string PaymentFailed = "ORD-4222";
    public const string CouponInvalid = "ORD-4223";
    public const string InsufficientPoints = "ORD-4224";
    public const string ReturnPeriodExpired = "ORD-4225";

    // 202 Accepted
    public const string PaymentPending = "ORD-2021";

    // 500/503 Server Error
    public const string InternalServerError = "ORD-5001";
    public const string ExternalServiceUnavailable = "ORD-5002";
    public const string SagaCompensating = "ORD-5003";
}
