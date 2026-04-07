namespace UserManagementService.DTOs.Responses;

/// <summary>ユーザープロフィルレスポンス DTO。パスワード等の秘密情報を含まない。</summary>
public record UserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    DateOnly? BirthDate,
    string Status,
    bool IsProcessingRestricted,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>住所レスポンス DTO。</summary>
public record AddressDto(
    string Id,
    string UserId,
    string AddressType,
    string Recipient,
    string ZipCode,
    string Prefecture,
    string City,
    string StreetAddress,
    string? Building,
    string? PhoneNumber,
    bool IsDefault,
    DateTimeOffset CreatedAt);

/// <summary>ウィッシュリストレスポンス DTO。アイテム一覧を含む。</summary>
public record WishlistDto(
    string Id,
    string UserId,
    string Name,
    bool IsDefault,
    List<WishlistItemDto> Items,
    DateTimeOffset CreatedAt);

/// <summary>ウィッシュリストアイテムレスポンス DTO。</summary>
public record WishlistItemDto(
    string Id,
    string ProductId,
    DateTimeOffset AddedAt,
    bool ShouldNotifyOnRestock,
    DateTimeOffset? NotifiedAt);

/// <summary>ユーザー設定レスポンス DTO。</summary>
public record PreferenceDto(
    string Id,
    string UserId,
    string Language,
    string Currency,
    string? NotificationPreferences,
    string? DisplayPreferences,
    DateTimeOffset UpdatedAt);

/// <summary>アクティビティレスポンス DTO（一般ユーザー向け、IP/端末情報なし）。</summary>
public record ActivityDto(
    string Id,
    string ActivityType,
    DateTimeOffset Timestamp,
    string? Details);

/// <summary>管理者向けアクティビティ DTO。IP アドレス・端末情報を含む。</summary>
public record AdminActivityDto(
    string Id,
    string ActivityType,
    DateTimeOffset Timestamp,
    string? Details,
    string? IpAddress,
    string? DeviceInfo);

/// <summary>GDPR 同意レスポンス DTO。</summary>
public record ConsentDto(
    string Id,
    string ConsentType,
    bool IsGranted,
    int Version,
    DateTimeOffset UpdatedAt);

/// <summary>会員ランクレスポンス DTO。</summary>
public record MemberRankDto(
    string Id,
    string UserId,
    string CurrentRank,
    decimal AnnualPurchaseAmount,
    decimal PreviousYearAmount,
    decimal PointRate,
    DateTimeOffset RankUpdatedAt,
    DateOnly NextEvaluationDate);

/// <summary>GDPR 削除リクエストレスポンス DTO。</summary>
public record DeletionRequestDto(
    string Id,
    string Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset GracePeriodEndsAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason);
