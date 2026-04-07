namespace PaymentCartService.Models.ValueObjects;

public record ShippingAddress(
    string RecipientName,
    string PostalCode,
    string Prefecture,
    string City,
    string AddressLine1,
    string? AddressLine2,
    string PhoneNumber);
