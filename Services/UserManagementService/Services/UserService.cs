using System.Text.Json;
using UserManagementService.DTOs.Requests;
using UserManagementService.DTOs.Responses;
using UserManagementService.Events;
using UserManagementService.Exceptions;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Services;

/// <summary>
/// ユーザー Aggregate のビジネスロジック。プロファイル CRUD、ステータス管理、データエクスポート要求を担当する。
/// Redis キャッシュによるプロファイル読み取りの高速化と、Outbox イベント発行を統合する。
/// </summary>
public class UserService(
    IUserRepository userRepository,
    IPreferenceRepository preferenceRepository,
    IMemberRankRepository memberRankRepository,
    IActivityRepository activityRepository,
    IEventPublisherService eventPublisher,
    ICacheService cacheService,
    TimeProvider timeProvider,
    ILogger<UserService> logger) : IUserService
{
    /// <summary>Redis キャッシュキーを生成する。形式: <c>user:profile:{userId}</c></summary>
    private static string ProfileCacheKey(string userId) => $"user:profile:{userId}";

    public async Task<UserDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var cached = await cacheService.GetAsync<UserDto>(ProfileCacheKey(id), ct);
        if (cached is not null)
            return cached;

        var user = await userRepository.FindByIdReadOnlyAsync(id, ct);
        if (user is null)
            return null;

        var dto = MapToDto(user);
        await cacheService.SetAsync(ProfileCacheKey(id), dto, TimeSpan.FromMinutes(30), ct);
        return dto;
    }

    public async Task<UserDto> UpdateProfileAsync(
        string id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(id, ct)
            ?? throw new NotFoundException($"ユーザーが見つかりません (ID: {id})");

        user.UpdateProfile(request.FirstName, request.LastName, request.PhoneNumber, request.BirthDate);

        await eventPublisher.PublishProfileUpdatedAsync(user.Id, ct);
        await userRepository.SaveChangesAsync(ct);
        await cacheService.RemoveAsync(ProfileCacheKey(id), ct);

        logger.LogInformation("ユーザープロファイルが更新されました: {UserId}", user.Id);
        return MapToDto(user);
    }

    /// <summary>
    /// AuthService の UserRegistered イベントを受信し、User・UserPreference・MemberRank を一括初期化する。
    /// べき等: 同一メールの User が存在する場合はスキップする。
    /// </summary>
    public async Task InitializeRegisteredUserAsync(
        UserRegisteredEvent @event, CancellationToken ct = default)
    {
        var existingUser = await userRepository.FindByEmailAsync(@event.Email, ct);
        if (existingUser is not null)
        {
            logger.LogWarning("ユーザーは既に存在します: {UserId}", @event.UserId);
            return;
        }

        var now = timeProvider.GetUtcNow();
        var user = User.CreateRegistered(@event.UserId, @event.Email, @event.FirstName, @event.LastName);
        var preference = new UserPreference
        {
            UserId = @event.UserId
        };
        var memberRank = MemberRank.CreateDefault(@event.UserId, now);

        await userRepository.AddAsync(user, ct);
        await preferenceRepository.AddAsync(preference, ct);
        await memberRankRepository.AddAsync(memberRank, ct);
        await eventPublisher.PublishProfileUpdatedAsync(user.Id, ct);
        await userRepository.SaveChangesAsync(ct);
        logger.LogInformation("登録ユーザー初期化が完了しました: {UserId}", user.Id);
    }

    public async Task UpdateStatusAsync(string id, string status, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(id, ct)
            ?? throw new NotFoundException($"ユーザーが見つかりません (ID: {id})");

        user.ChangeStatus(status);
        await userRepository.SaveChangesAsync(ct);
        await cacheService.RemoveAsync(ProfileCacheKey(id), ct);
        logger.LogInformation("ユーザーステータスが更新されました: {UserId}, {Status}", id, status);
    }

    public async Task SetProcessingRestrictionAsync(
        string id, bool restricted, string? reason, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(id, ct)
            ?? throw new NotFoundException($"ユーザーが見つかりません (ID: {id})");

        user.SetProcessingRestriction(restricted, reason, restricted ? timeProvider.GetUtcNow() : null);
        await userRepository.SaveChangesAsync(ct);
        await cacheService.RemoveAsync(ProfileCacheKey(id), ct);
        logger.LogInformation("処理制限が更新されました: {UserId}, Restricted={Restricted}", id, restricted);
    }

    /// <summary>
    /// 管理者によるステータス変更。変更操作を UserActivity に監査ログとして記録する。
    /// </summary>
    public async Task UpdateStatusByAdminAsync(
        string id, string status, string adminId, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(id, ct)
            ?? throw new NotFoundException($"ユーザーが見つかりません (ID: {id})");

        user.ChangeStatus(status);
        await activityRepository.AddAsync(new UserActivity
        {
            UserId = id,
            ActivityType = "ADMIN_STATUS_CHANGED",
            Timestamp = timeProvider.GetUtcNow(),
            Details = JsonSerializer.Serialize(new { AdminId = adminId, NewStatus = status })
        }, ct);
        await userRepository.SaveChangesAsync(ct);
        await cacheService.RemoveAsync(ProfileCacheKey(id), ct);
        logger.LogInformation("管理者によるユーザーステータス更新: {UserId}, {Status}", id, status);
    }

    /// <summary>
    /// 管理者による処理制限の設定・解除。GDPR Art.18 に基づき、制限理由を監査ログに記録する。
    /// </summary>
    public async Task UpdateProcessingRestrictionByAdminAsync(
        string id,
        bool restricted,
        string? reason,
        string adminId,
        CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(id, ct)
            ?? throw new NotFoundException($"ユーザーが見つかりません (ID: {id})");

        user.SetProcessingRestriction(restricted, reason, restricted ? timeProvider.GetUtcNow() : null);

        await activityRepository.AddAsync(new UserActivity
        {
            UserId = id,
            ActivityType = restricted
                ? "ADMIN_PROCESSING_RESTRICTION_SET"
                : "ADMIN_PROCESSING_RESTRICTION_REMOVED",
            Timestamp = timeProvider.GetUtcNow(),
            Details = JsonSerializer.Serialize(new { AdminId = adminId, Reason = reason })
        }, ct);

        await userRepository.SaveChangesAsync(ct);
        await cacheService.RemoveAsync(ProfileCacheKey(id), ct);
        logger.LogInformation("管理者による処理制限更新: {UserId}, Restricted={Restricted}", id, restricted);
    }

    public async Task<(List<UserDto> Items, int TotalCount)> GetAllAsync(
        int page, int pageSize, string? statusFilter, CancellationToken ct = default)
    {
        var (users, totalCount) = await userRepository.FindAllAsync(page, pageSize, statusFilter, ct);
        return (users.Select(MapToDto).ToList(), totalCount);
    }

    public async Task ResetLastLoginAtAsync(string userId, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(userId, ct)
            ?? throw new NotFoundException($"User {userId} not found");
        user.ResetLastLogin();
        await userRepository.SaveChangesAsync(ct);
        await cacheService.RemoveAsync(ProfileCacheKey(userId), ct);
        logger.LogInformation("LastLoginAtをリセットしました: {UserId}", userId);
    }

    /// <summary>
    /// GDPR Art.20 データポータビリティ要求。24 時間以内の再リクエストを制限する。
    /// <see cref="BackgroundServices.DataExportService"/> が非同期でエクスポートを実行する。
    /// </summary>
    public async Task RequestDataExportAsync(string userId, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(userId, ct)
            ?? throw new NotFoundException($"User {userId} not found");

        if (user.IsDataExportRequested)
            throw new BusinessException("データエクスポートは既にリクエスト済みです");

        if (user.DataExportCompletedAt is not null
            && user.DataExportCompletedAt > timeProvider.GetUtcNow().AddHours(-24))
            throw new BusinessException("エクスポートは 24 時間に 1 回のみリクエスト可能です");

        user.RequestDataExport();
        await userRepository.SaveChangesAsync(ct);
        logger.LogInformation("データエクスポートをリクエストしました: {UserId}", userId);
    }

    /// <summary>User エンティティを UserDto に変換するマッピングヘルパー。</summary>
    private static UserDto MapToDto(User user) =>
        new(user.Id, user.Email, user.FirstName, user.LastName,
            user.PhoneNumber, user.BirthDate, user.Status,
            user.IsProcessingRestricted, user.LastLoginAt,
            user.CreatedAt, user.UpdatedAt);
}
