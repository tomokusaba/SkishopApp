using UserManagementService.DTOs.Requests;
using UserManagementService.DTOs.Responses;

namespace UserManagementService.Services.Interfaces;

/// <summary>
/// ユーザー設定サービス。言語・通貨・通知・表示プリファレンスを管理する。
/// </summary>
public interface IPreferenceService
{
    Task<PreferenceDto?> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<PreferenceDto> UpdateAsync(string userId, UpdatePreferenceRequest request, CancellationToken ct = default);
    Task InitializeAsync(string userId, CancellationToken ct = default);
}
