using Frontend.DTOs;
using Frontend.Models;
using Frontend.Services.Interfaces;

namespace Frontend.Services;

/// <summary>
/// ユーザー管理 API クライアント（§9.2 全エンドポイント対応）
/// BFF パターン: サーバーサイドから API Gateway 経由でユーザー管理サービスに通信
/// </summary>
public class UserApiClient(
    IApiGatewayClient apiClient,
    ILogger<UserApiClient> logger) : IUserApiClient
{
    // --- プロフィール ---

    public async Task<UserProfileDto?> GetMyProfileAsync(CancellationToken ct = default)
    {
        return await apiClient.GetAsync<UserProfileDto>("/api/v1/users/me", ct);
    }

    public async Task<UserProfileDto?> UpdateMyProfileAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("プロフィール更新");
        return await apiClient.PutAsync<UpdateProfileRequest, UserProfileDto>("/api/v1/users/me", request, ct);
    }

    // --- 住所管理 ---

    public async Task<List<AddressDto>> GetAddressesAsync(string userId, CancellationToken ct = default)
    {
        var result = await apiClient.GetAsync<List<AddressDto>>($"/api/v1/users/{userId}/addresses", ct);
        return result ?? [];
    }

    public async Task<AddressDto?> AddAddressAsync(string userId, CreateAddressRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("住所追加: UserId={UserId}", userId);
        return await apiClient.PostAsync<CreateAddressRequest, AddressDto>(
            $"/api/v1/users/{userId}/addresses", request, ct);
    }

    public async Task<AddressDto?> UpdateAddressAsync(string userId, string id, UpdateAddressRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("住所更新: UserId={UserId}, AddressId={AddressId}", userId, id);
        return await apiClient.PutAsync<UpdateAddressRequest, AddressDto>(
            $"/api/v1/users/{userId}/addresses/{id}", request, ct);
    }

    public async Task DeleteAddressAsync(string userId, string id, CancellationToken ct = default)
    {
        logger.LogInformation("住所削除: UserId={UserId}, AddressId={AddressId}", userId, id);
        await apiClient.DeleteAsync($"/api/v1/users/{userId}/addresses/{id}", ct);
    }

    // --- アクティビティ ---

    public async Task<PaginatedResult<ActivityDto>> GetMyActivitiesAsync(int page = 0, int size = 20, CancellationToken ct = default)
    {
        return await apiClient.GetPaginatedAsync<ActivityDto>("/api/v1/users/me/activities", page, size, ct: ct);
    }

    // --- ユーザー設定 ---

    public async Task<UserPreferencesDto?> GetPreferencesAsync(string userId, CancellationToken ct = default)
    {
        return await apiClient.GetAsync<UserPreferencesDto>($"/api/v1/users/{userId}/preferences", ct);
    }

    public async Task<UserPreferencesDto?> UpdatePreferencesAsync(string userId, UserPreferencesDto request, CancellationToken ct = default)
    {
        logger.LogInformation("ユーザー設定更新: UserId={UserId}", userId);
        return await apiClient.PutAsync<UserPreferencesDto, UserPreferencesDto>(
            $"/api/v1/users/{userId}/preferences", request, ct);
    }

    // --- 会員ランク ---

    public async Task<MemberRankDto?> GetMemberRankAsync(string userId, CancellationToken ct = default)
    {
        return await apiClient.GetAsync<MemberRankDto>($"/api/v1/users/{userId}/member-rank", ct);
    }

    // --- 同意管理 ---

    public async Task<List<ConsentDto>> GetConsentsAsync(string userId, CancellationToken ct = default)
    {
        var result = await apiClient.GetAsync<List<ConsentDto>>($"/api/v1/users/{userId}/consents", ct);
        return result ?? [];
    }

    public async Task UpdateConsentsAsync(string userId, UpdateConsentsRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("同意情報更新: UserId={UserId}", userId);
        await apiClient.PutAsync<UpdateConsentsRequest, object>(
            $"/api/v1/users/{userId}/consents", request, ct);
    }

    // --- データエクスポート ---

    public async Task<DataExportDto?> RequestDataExportAsync(string userId, CancellationToken ct = default)
    {
        logger.LogInformation("データエクスポート要求: UserId={UserId}", userId);
        return await apiClient.PostAsync<object, DataExportDto>(
            $"/api/v1/users/{userId}/data-export", new { }, ct);
    }

    public async Task<DataExportDto?> GetDataExportStatusAsync(string userId, string requestId, CancellationToken ct = default)
    {
        return await apiClient.GetAsync<DataExportDto>(
            $"/api/v1/users/{userId}/data-export/{requestId}", ct);
    }

    public async Task<byte[]> DownloadDataExportAsync(string userId, string requestId, CancellationToken ct = default)
    {
        logger.LogInformation("データエクスポートダウンロード: UserId={UserId}, RequestId={RequestId}", userId, requestId);
        var response = await apiClient.SendAsync(
            HttpMethod.Get,
            $"/api/v1/users/{userId}/data-export/{requestId}/download",
            ct: ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    // --- アカウント削除 ---

    public async Task<DeletionRequestDto?> RequestAccountDeletionAsync(string userId, RequestDeletionRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("アカウント削除要求: UserId={UserId}", userId);
        return await apiClient.PostAsync<RequestDeletionRequest, DeletionRequestDto>(
            $"/api/v1/users/{userId}/deletion-request", request, ct);
    }

    public async Task<DeletionRequestDto?> GetDeletionRequestStatusAsync(string userId, CancellationToken ct = default)
    {
        return await apiClient.GetAsync<DeletionRequestDto>(
            $"/api/v1/users/{userId}/deletion-request", ct);
    }

    public async Task CancelDeletionRequestAsync(string userId, CancellationToken ct = default)
    {
        logger.LogInformation("アカウント削除取り消し: UserId={UserId}", userId);
        await apiClient.PostAsync<object, object>(
            $"/api/v1/users/{userId}/deletion-request/cancel", new { }, ct);
    }

    // --- パスワード変更 ---

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("パスワード変更");
        await apiClient.PutAsync<ChangePasswordRequest, object>("/api/v1/users/me/password", request, ct);
    }
}

