using System.ComponentModel.DataAnnotations;

namespace SalesManagementService.DTOs.Requests;

public record ShippingAddressRequest(
    [Required, StringLength(100)] string RecipientName,
    [Required, StringLength(10)] string PostalCode,
    [Required, StringLength(50)] string Prefecture,
    [Required, StringLength(100)] string City,
    [Required, StringLength(200)] string AddressLine1,
    [StringLength(200)] string? AddressLine2,
    [Required, StringLength(20)] string PhoneNumber);
