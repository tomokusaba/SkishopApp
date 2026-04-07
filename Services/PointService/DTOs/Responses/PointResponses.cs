namespace PointService.DTOs.Responses;

public record PointBalanceResponse(
    string UserId, int AvailablePoints, int PendingPoints,
    int TotalEarned, int TotalSpent, int TotalExpired);

public record PointTransactionResponse(
    string Id, string Type, int Points, int BalanceAfter,
    string? ReferenceId, string? ReferenceType,
    string? Description, DateTime? ExpiresAt, DateTime CreatedAt);

public record TierInfoResponse(
    string TierName, decimal EarnRateMultiplier,
    int TotalEarnedPoints, int CurrentYearPoints,
    string? NextTierName, int? PointsToNextTier,
    List<string> Benefits);

public record ExpiringPointsResponse(
    List<ExpiringPointItem> Items, int TotalExpiringPoints);

public record ExpiringPointItem(
    int Points, DateTime ExpiresAt, string SourceDescription);

public record PointAnalyticsResponse(
    long TotalPointsIssued, long TotalPointsRedeemed, long TotalPointsExpired,
    double RedemptionRate, Dictionary<string, long> PointsByTier,
    Dictionary<string, long> TierDistribution);

public record ReservePointsResult(
    bool Success, int RemainingBalance, string? ErrorMessage = null);

public record PagedResult<T>(
    List<T> Items, int TotalCount, int Page, int PageSize);

public record TierDefinitionResponse(
    string Id, string Name, decimal PointRate,
    int MinAnnualPoints, string? Benefits, int SortOrder);

public record ConfirmPointsResponse(int ConfirmedPoints);
public record ReleasePointsResponse(int ReleasedPoints);
public record AwardPointsResponse(int EarnedPoints);
public record EarnPointsResult(int EarnedPoints, int NewBalance);
