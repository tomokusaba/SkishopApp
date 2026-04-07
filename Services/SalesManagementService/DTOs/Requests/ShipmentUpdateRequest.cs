using System.ComponentModel.DataAnnotations;

namespace SalesManagementService.DTOs.Requests;

public record ShipmentUpdateRequest(
    [Required, StringLength(20)] string Status,
    [StringLength(100)] string? TrackingNumber = null);
