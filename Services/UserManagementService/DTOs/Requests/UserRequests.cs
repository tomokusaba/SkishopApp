using System.ComponentModel.DataAnnotations;

namespace UserManagementService.DTOs.Requests;

/// <summary>ユーザープロフィル更新リクエスト。null のフィールドは更新しない（部分更新）。</summary>
public record UpdateUserRequest(
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    DateOnly? BirthDate);

/// <summary>住所新規登録リクエスト。全フィールド必須。</summary>
public record CreateAddressRequest(
    [Required] string AddressType,
    [Required, MaxLength(100)] string Recipient,
    [Required, MaxLength(10)] string ZipCode,
    [Required, MaxLength(50)] string Prefecture,
    [Required, MaxLength(100)] string City,
    [Required, MaxLength(255)] string StreetAddress,
    [MaxLength(255)] string? Building,
    [MaxLength(20)] string? PhoneNumber);

/// <summary>住所部分更新リクエスト。null のフィールドは更新しない。</summary>
public record UpdateAddressRequest(
    string? Recipient,
    string? ZipCode,
    string? Prefecture,
    string? City,
    string? StreetAddress,
    string? Building,
    string? PhoneNumber,
    bool? IsDefault);

/// <summary>ウィッシュリスト新規作成リクエスト。</summary>
public record CreateWishlistRequest(
    [Required, MaxLength(100)] string Name,
    bool IsDefault = false);

/// <summary>ウィッシュリスト部分更新リクエスト。</summary>
public record UpdateWishlistRequest(
    string? Name,
    bool? IsDefault);

/// <summary>ウィッシュリストアイテム追加リクエスト。</summary>
public record AddWishlistItemRequest(
    [Required, MaxLength(36)] string ProductId,
    bool ShouldNotifyOnRestock = false);

/// <summary>ユーザー設定更新リクエスト。言語・通貨・通知・表示設定。</summary>
public record UpdatePreferenceRequest(
    string? Language,
    string? Currency,
    string? NotificationPreferences,
    string? DisplayPreferences);

/// <summary>GDPR 同意更新リクエスト。同意の付与/撤回を行う。</summary>
public record ConsentUpdateRequest(
    [Required] string ConsentType,
    bool IsGranted,
    int PolicyVersion);

/// <summary>GDPR アカウント削除リクエスト作成用。</summary>
public record CreateDeletionRequest(
    [Required, MaxLength(30)] string RequestChannel);

/// <summary>管理者によるユーザーステータス変更リクエスト。</summary>
public record UpdateUserStatusRequest(
    [Required, MaxLength(50)] string Status);

/// <summary>GDPR Article 18 に基づく処理制限設定リクエスト。</summary>
public record UpdateProcessingRestrictionRequest(
    bool IsProcessingRestricted,
    [MaxLength(500)] string? RestrictionReason);
