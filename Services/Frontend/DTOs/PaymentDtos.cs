namespace Frontend.DTOs;

public record GuestCheckoutRequest(
    string Email,
    string EmailConfirmation,
    GuestShippingAddress ShippingAddress,
    string ShippingMethod,
    string CartId,
    bool ConsentOverseasTransfer,
    bool ConsentPrivacyPolicy);

public record GuestShippingAddress(
    string FullName,
    string PostalCode,
    string Prefecture,
    string City,
    string AddressLine1,
    string? AddressLine2,
    string PhoneNumber);

public record GuestCheckoutResponse(
    string OrderId,
    string? CheckoutSessionUrl);

public record PaymentDto(
    string Id,
    string OrderId,
    string Status,
    decimal Amount,
    string? TransactionId,
    DateTime CreatedAt);
