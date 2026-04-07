using UserManagementService.DTOs.Responses;

namespace UserManagementService.Services.Interfaces;

/// <summary>
/// ユーザーアクティビティ履歴サービス。操作履歴の記録と取得を提供する。
/// </summary>
public interface IActivityService
{
    Task<(List<ActivityDto> Items, int TotalCount)> GetByUserIdAsync(string userId, int page, int pageSize, CancellationToken ct = default);
    Task RecordAsync(string userId, string activityType, string? details, string? ipAddress, string? deviceInfo, CancellationToken ct = default);
}
