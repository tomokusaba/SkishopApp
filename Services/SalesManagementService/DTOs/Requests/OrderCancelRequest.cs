using System.ComponentModel.DataAnnotations;

namespace SalesManagementService.DTOs.Requests;

public record OrderCancelRequest([Required, StringLength(500)] string Reason);
