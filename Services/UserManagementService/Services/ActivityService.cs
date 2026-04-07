using UserManagementService.DTOs.Responses;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Services;

/// <summary>
/// ユーザーアクティビティ（行動履歴）のビジネスロジック。ページネーション付き取得と記録を担当する。
/// </summary>
public class ActivityService(
    IActivityRepository activityRepository,
    TimeProvider timeProvider,
    ILogger<ActivityService> logger) : IActivityService
{
    public async Task<(List<ActivityDto> Items, int TotalCount)> GetByUserIdAsync(
        string userId, int page, int pageSize, CancellationToken ct = default)
    {
        var (activities, totalCount) = await activityRepository.FindByUserIdAsync(userId, page, pageSize, ct);
        return (activities.Select(MapToDto).ToList(), totalCount);
    }

    public async Task RecordAsync(
        string userId, string activityType, string? details,
        string? ipAddress, string? deviceInfo, CancellationToken ct = default)
    {
        var activity = new UserActivity
        {
            UserId = userId,
            ActivityType = activityType,
            Timestamp = timeProvider.GetUtcNow(),
            Details = details,
            IpAddress = ipAddress,
            DeviceInfo = deviceInfo
        };

        await activityRepository.AddAsync(activity, ct);
        await activityRepository.SaveChangesAsync(ct);
        logger.LogInformation("アクティビティが記録されました: {UserId}, {ActivityType}", userId, activityType);
    }

    private static ActivityDto MapToDto(UserActivity a) =>
        new(a.Id, a.ActivityType, a.Timestamp, a.Details);
}
