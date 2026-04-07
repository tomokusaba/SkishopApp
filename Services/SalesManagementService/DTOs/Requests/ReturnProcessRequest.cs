using System.ComponentModel.DataAnnotations;

namespace SalesManagementService.DTOs.Requests;

public record ReturnProcessRequest(
    [Required, StringLength(20)] string Status,
    [StringLength(2000)] string? AdminNotes = null);
