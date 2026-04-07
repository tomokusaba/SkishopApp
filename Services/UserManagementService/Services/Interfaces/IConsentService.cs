using UserManagementService.DTOs.Requests;
using UserManagementService.DTOs.Responses;

namespace UserManagementService.Services.Interfaces;

/// <summary>
/// GDPR 同意管理サービス。同意の取得・更新・匠名同意作成を提供する。
/// </summary>
public interface IConsentService
{
    Task<List<ConsentDto>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<ConsentDto> UpdateAsync(string userId, ConsentUpdateRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task<ConsentDto> CreateAnonymousConsentAsync(ConsentUpdateRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default);
}
