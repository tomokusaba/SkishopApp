using UserManagementService.DTOs.Requests;
using UserManagementService.DTOs.Responses;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Services;

/// <summary>
/// GDPR 同意管理のビジネスロジック。同意付与・撤回・匿名同意を処理し、
/// 撤回時に consent.revoked Outbox イベントを発行する。
/// </summary>
public class ConsentService(
    IConsentRepository consentRepository,
    IEventPublisherService eventPublisher,
    ILogger<ConsentService> logger) : IConsentService
{
    public async Task<List<ConsentDto>> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        var consents = await consentRepository.FindByUserIdAsync(userId, ct);
        return consents.Select(MapToDto).ToList();
    }

    public async Task<ConsentDto> UpdateAsync(
        string userId, ConsentUpdateRequest request,
        string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        var consent = await consentRepository.FindByUserIdAndTypeAsync(userId, request.ConsentType, ct);

        if (consent is null)
        {
            consent = new Consent
            {
                UserId = userId,
                ConsentType = request.ConsentType,
                IsGranted = request.IsGranted,
                Version = request.PolicyVersion,
                IpAddress = ipAddress,
                UserAgent = userAgent
            };
            await consentRepository.AddAsync(consent, ct);
        }
        else
        {
            var wasGranted = consent.IsGranted;

            if (request.IsGranted)
            {
                consent.Grant(ipAddress, userAgent, null);
            }
            else
            {
                consent.Revoke(ipAddress, userAgent);
            }

            if (wasGranted && !request.IsGranted)
            {
                await eventPublisher.PublishConsentRevokedAsync(userId, request.ConsentType, ct);
                logger.LogInformation("同意が撤回されました: {UserId}, {ConsentType}", userId, request.ConsentType);
            }
        }

        await consentRepository.SaveChangesAsync(ct);
        logger.LogInformation("同意が更新されました: {UserId}, {ConsentType}, Granted={IsGranted}",
            userId, request.ConsentType, request.IsGranted);
        return MapToDto(consent);
    }

    /// <summary>
    /// 未ログインユーザーの同意を記録する（Cookie 同意バナー等）。userId は "anonymous" 固定。
    /// </summary>
    public async Task<ConsentDto> CreateAnonymousConsentAsync(
        ConsentUpdateRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        var consent = new Consent
        {
            UserId = "anonymous",
            ConsentType = request.ConsentType,
            IsGranted = request.IsGranted,
            Version = request.PolicyVersion,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
        await consentRepository.AddAsync(consent, ct);
        await consentRepository.SaveChangesAsync(ct);
        logger.LogInformation("匿名同意が記録されました: {ConsentType}, Granted={IsGranted}",
            request.ConsentType, request.IsGranted);
        return MapToDto(consent);
    }

    private static ConsentDto MapToDto(Consent c) =>
        new(c.Id, c.ConsentType, c.IsGranted, c.Version, c.UpdatedAt);
}
