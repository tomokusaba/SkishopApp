using UserManagementService.DTOs.Requests;
using UserManagementService.DTOs.Responses;
using UserManagementService.Exceptions;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Services;

/// <summary>
/// GDPR Art.17 データ主体権利（DSR: Data Subject Request）のビジネスロジック。
/// 14 日間の猶予期間付き削除リクエストのライフサイクル管理と、Saga 連携を担当する。
/// </summary>
public class DsrService(
    IDeletionRequestRepository deletionRequestRepository,
    IUserRepository userRepository,
    IEventPublisherService eventPublisher,
    TimeProvider timeProvider,
    ILogger<DsrService> logger) : IDsrService
{
    /// <summary>GDPR 削除リクエストの猶予期間（14 日間）。この期間中はキャンセル可能。</summary>
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(14);

    public async Task<DeletionRequestDto> CreateDeletionRequestAsync(
        string userId, string requestedBy, CreateDeletionRequest request, CancellationToken ct = default)
    {
        var existingRequest = await deletionRequestRepository.FindPendingByUserIdAsync(userId, ct);
        if (existingRequest is not null)
            throw new BusinessException("既に削除リクエストが存在します");

        var now = timeProvider.GetUtcNow();
        var deletionRequest = new DeletionRequest
        {
            UserId = userId,
            RequestedBy = requestedBy,
            RequestChannel = request.RequestChannel,
            RequestedAt = now,
            GracePeriodEndsAt = now.Add(GracePeriod),
            Status = DeletionRequestStatus.Pending
        };

        await deletionRequestRepository.AddAsync(deletionRequest, ct);
        await deletionRequestRepository.SaveChangesAsync(ct);
        logger.LogInformation("削除リクエストが作成されました: {RequestId}, {UserId}", deletionRequest.Id, userId);
        return MapToDto(deletionRequest);
    }

    public async Task<DeletionRequestDto?> GetDeletionRequestAsync(string userId, CancellationToken ct = default)
    {
        var request = await deletionRequestRepository.FindPendingByUserIdAsync(userId, ct);
        return request is null ? null : MapToDto(request);
    }

    public async Task CancelDeletionRequestAsync(string userId, CancellationToken ct = default)
    {
        var request = await deletionRequestRepository.FindPendingByUserIdAsync(userId, ct)
            ?? throw new NotFoundException("削除リクエストが見つかりません");

        request.Cancel();
        await deletionRequestRepository.SaveChangesAsync(ct);
        logger.LogInformation("削除リクエストがキャンセルされました: {RequestId}, {UserId}", request.Id, userId);
    }

    /// <summary>
    /// 猶予期間が経過した削除リクエストを PROCESSING に遷移させ、user.deleted イベントを発行する。
    /// <see cref="BackgroundServices.DsrTimeoutMonitorService"/> から定期呼び出しされる。
    /// </summary>
    public async Task ProcessExpiredGracePeriodRequestsAsync(CancellationToken ct = default)
    {
        var expiredRequests = await deletionRequestRepository.FindExpiredGracePeriodAsync(ct);

        foreach (var request in expiredRequests)
        {
            request.StartProcessing();
            if (request.UserId is not null)
            {
                await eventPublisher.PublishUserDeletedAsync(request.UserId, ct);
            }
        }

        await deletionRequestRepository.SaveChangesAsync(ct);
        logger.LogInformation("猶予期間切れリクエスト処理: {Count} 件", expiredRequests.Count);
    }

    /// <summary>
    /// 他マイクロサービスからの削除完了/失敗通知を処理する。成功時は COMPLETED に遷移し通知イベントを発行する。
    /// </summary>
    public async Task HandleDeletionCompletedAsync(
        string userId, string serviceName, bool success, string? error, CancellationToken ct = default)
    {
        var request = await deletionRequestRepository.FindPendingByUserIdAsync(userId, ct);
        if (request is null)
        {
            logger.LogWarning("削除完了通知の対象リクエストが見つかりません: {UserId}, {ServiceName}", userId, serviceName);
            return;
        }

        if (!success)
        {
            request.Fail($"{serviceName}: {error}");
            logger.LogWarning("削除処理が失敗しました: {UserId}, {ServiceName}, {Error}", userId, serviceName, error);
        }
        else
        {
            request.Complete(timeProvider.GetUtcNow());

            var user = await userRepository.FindByIdAsync(userId, ct);
            if (user is not null)
            {
                await eventPublisher.PublishDeletionNotificationAsync(userId, ct);
            }

            logger.LogInformation("削除処理が完了しました: {UserId}", userId);
        }

        await deletionRequestRepository.SaveChangesAsync(ct);
    }

    private static DeletionRequestDto MapToDto(DeletionRequest d) =>
        new(d.Id, d.Status, d.RequestedAt, d.GracePeriodEndsAt, d.CompletedAt, d.FailureReason);

    /// <summary>
    /// 24 時間以上 PROCESSING のまま完了しないリクエストをリトライまたは FAILED に遷移させる。
    /// 最大 3 回リトライ後に FAILED へ遷移する。
    /// </summary>
    public async Task ProcessTimedOutRequestsAsync(CancellationToken ct = default)
    {
        var processingRequests = await deletionRequestRepository
            .FindTimedOutProcessingAsync(TimeSpan.FromHours(24), ct);

        foreach (var request in processingRequests)
        {
            request.IncrementRetry(3, $"タイムアウト: 3 回のリトライ後も完了せず");
            if (request.Status == DeletionRequestStatus.Failed)
                logger.LogWarning("DSR タイムアウト（最大リトライ到達）: {RequestId}", request.Id);
            else
                logger.LogWarning("DSR タイムアウトリトライ: {RequestId}, RetryCount={RetryCount}",
                    request.Id, request.RetryCount);
        }

        await deletionRequestRepository.SaveChangesAsync(ct);
    }
}
