using UserManagementService.DTOs.Requests;
using UserManagementService.DTOs.Responses;

namespace UserManagementService.Services.Interfaces;

/// <summary>
/// GDPR DSR（Data Subject Request）サービス。削除リクエストの作成・取消・献予期間処理・タイムアウト監視を提供する。
/// </summary>
public interface IDsrService
{
    Task<DeletionRequestDto> CreateDeletionRequestAsync(string userId, string requestedBy, CreateDeletionRequest request, CancellationToken ct = default);
    Task<DeletionRequestDto?> GetDeletionRequestAsync(string userId, CancellationToken ct = default);
    Task CancelDeletionRequestAsync(string userId, CancellationToken ct = default);
    Task ProcessExpiredGracePeriodRequestsAsync(CancellationToken ct = default);
    Task HandleDeletionCompletedAsync(string userId, string serviceName, bool success, string? error, CancellationToken ct = default);
    Task ProcessTimedOutRequestsAsync(CancellationToken ct = default);
}
