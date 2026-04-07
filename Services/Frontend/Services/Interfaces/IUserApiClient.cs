using Frontend.DTOs;
using Frontend.Models;

namespace Frontend.Services.Interfaces;

/// <summary>
/// ユーザー管理 API クライアントインターフェース（§9.2 全エンドポイント対応）
/// BFF パターン: サーバーサイドから API Gateway 経由でユーザー管理サービスに通信
/// </summary>
public interface IUserApiClient
{
    // --- プロフィール ---
    Task<UserProfileDto?> GetMyProfileAsync(CancellationToken ct = default);
    Task<UserProfileDto?> UpdateMyProfileAsync(UpdateProfileRequest request, CancellationToken ct = default);

    // --- 住所管理 ---
    Task<List<AddressDto>> GetAddressesAsync(string userId, CancellationToken ct = default);
    Task<AddressDto?> AddAddressAsync(string userId, CreateAddressRequest request, CancellationToken ct = default);
    Task<AddressDto?> UpdateAddressAsync(string userId, string id, UpdateAddressRequest request, CancellationToken ct = default);
    Task DeleteAddressAsync(string userId, string id, CancellationToken ct = default);

    // --- アクティビティ ---
    Task<PaginatedResult<ActivityDto>> GetMyActivitiesAsync(int page = 0, int size = 20, CancellationToken ct = default);

    // --- ユーザー設定 ---
    Task<UserPreferencesDto?> GetPreferencesAsync(string userId, CancellationToken ct = default);
    Task<UserPreferencesDto?> UpdatePreferencesAsync(string userId, UserPreferencesDto request, CancellationToken ct = default);

    // --- 会員ランク ---
    Task<MemberRankDto?> GetMemberRankAsync(string userId, CancellationToken ct = default);

    // --- 同意管理 ---
    Task<List<ConsentDto>> GetConsentsAsync(string userId, CancellationToken ct = default);
    Task UpdateConsentsAsync(string userId, UpdateConsentsRequest request, CancellationToken ct = default);

    // --- データエクスポート ---
    Task<DataExportDto?> RequestDataExportAsync(string userId, CancellationToken ct = default);
    Task<DataExportDto?> GetDataExportStatusAsync(string userId, string requestId, CancellationToken ct = default);
    Task<byte[]> DownloadDataExportAsync(string userId, string requestId, CancellationToken ct = default);

    // --- アカウント削除 ---
    Task<DeletionRequestDto?> RequestAccountDeletionAsync(string userId, RequestDeletionRequest request, CancellationToken ct = default);
    Task<DeletionRequestDto?> GetDeletionRequestStatusAsync(string userId, CancellationToken ct = default);
    Task CancelDeletionRequestAsync(string userId, CancellationToken ct = default);

    // --- パスワード変更 ---
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);
}
