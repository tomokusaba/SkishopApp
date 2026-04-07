using System.ComponentModel.DataAnnotations;

namespace SalesManagementService.DTOs.Requests;

public record ShipmentCreateRequest(
    [Required, StringLength(36)] string OrderId,
    [Required, StringLength(100)] string Carrier,
    [StringLength(100)] string? TrackingNumber = null);
