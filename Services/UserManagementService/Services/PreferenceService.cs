using UserManagementService.DTOs.Requests;
using UserManagementService.DTOs.Responses;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Services;

/// <summary>
/// ユーザー設定（言語・通貨・通知・表示）のビジネスロジック。
/// 未存在時はデフォルト値で自動作成する。
/// </summary>
public class PreferenceService(
    IPreferenceRepository preferenceRepository,
    ILogger<PreferenceService> logger) : IPreferenceService
{
    public async Task<PreferenceDto?> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        var pref = await preferenceRepository.FindByUserIdAsync(userId, ct);
        return pref is null ? null : MapToDto(pref);
    }

    public async Task<PreferenceDto> UpdateAsync(
        string userId, UpdatePreferenceRequest request, CancellationToken ct = default)
    {
        var pref = await preferenceRepository.FindByUserIdAsync(userId, ct);
        if (pref is null)
        {
            pref = new UserPreference { UserId = userId };
            await preferenceRepository.AddAsync(pref, ct);
        }

        if (request.Language is not null) pref.Language = request.Language;
        if (request.Currency is not null) pref.Currency = request.Currency;
        if (request.NotificationPreferences is not null) pref.NotificationPreferences = request.NotificationPreferences;
        if (request.DisplayPreferences is not null) pref.DisplayPreferences = request.DisplayPreferences;

        await preferenceRepository.SaveChangesAsync(ct);
        logger.LogInformation("ユーザー設定が更新されました: {UserId}", userId);
        return MapToDto(pref);
    }

    public async Task InitializeAsync(string userId, CancellationToken ct = default)
    {
        var existing = await preferenceRepository.FindByUserIdAsync(userId, ct);
        if (existing is not null) return;

        var pref = new UserPreference { UserId = userId };
        await preferenceRepository.AddAsync(pref, ct);
        await preferenceRepository.SaveChangesAsync(ct);
        logger.LogInformation("ユーザー設定が初期化されました: {UserId}", userId);
    }

    private static PreferenceDto MapToDto(UserPreference p) =>
        new(p.Id, p.UserId, p.Language, p.Currency,
            p.NotificationPreferences, p.DisplayPreferences, p.UpdatedAt);
}
