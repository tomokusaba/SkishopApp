namespace Frontend.DTOs;

public record UserProfileDto(string Id, string Email, string Name, string? PhoneNumber, string Role, DateTime CreatedAt);
public record UpdateProfileRequest(string Name, string? PhoneNumber);

public record AddressDto(
    string Id, string UserId, string Label, string PostalCode, string Prefecture,
    string City, string AddressLine1, string? AddressLine2, string PhoneNumber, bool IsDefault);
public record CreateAddressRequest(
    string Label, string PostalCode, string Prefecture, string City,
    string AddressLine1, string? AddressLine2, string PhoneNumber, bool IsDefault);
public record UpdateAddressRequest(
    string Label, string PostalCode, string Prefecture, string City,
    string AddressLine1, string? AddressLine2, string PhoneNumber, bool IsDefault);

public record ActivityDto(string Id, string ActionType, string Description, string IpAddress, DateTime OccurredAt);

public record UserPreferencesDto(
    bool OrderConfirmationEmail, bool ShippingNotificationEmail,
    bool PromotionEmail, bool PointExpiryEmail, string Language, string Theme);

public record MemberRankDto(string RankName, string Description, int CurrentPoints, int NextRankThreshold, List<string> Benefits);

public record ConsentDto(string ConsentType, bool IsGranted, string Description, DateTime? UpdatedAt);
public record UpdateConsentsRequest(List<ConsentUpdateItem> Consents);
public record ConsentUpdateItem(string ConsentType, bool IsGranted);

public record DataExportDto(string RequestId, string Status, DateTime RequestedAt, string? DownloadUrl);
public record DeletionRequestDto(string RequestId, string Status, DateTime RequestedAt, string? Reason);
public record RequestDeletionRequest(string Password, string? Reason);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
