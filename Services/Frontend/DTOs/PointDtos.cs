namespace Frontend.DTOs;

public record PointBalanceDto(int TotalPoints, int AvailablePoints, int PendingPoints);

public record PointHistoryDto(
    string Id,
    string TransactionType,
    int Points,
    string Description,
    string? OrderId,
    DateTime OccurredAt);

public record PointHistoryResult(
    List<PointHistoryDto> Items,
    long TotalElements,
    int Page,
    int Size)
{
    public int TotalPages => Size > 0 ? (int)Math.Ceiling((double)TotalElements / Size) : 0;
}

public record TierInfoDto(
    string CurrentTier,
    string? NextTier,
    int CurrentPoints,
    int NextTierThreshold,
    int PointsToNextTier,
    List<string> Benefits);

public record ExpiringPointsDto(int ExpiringPoints, DateTime ExpiryDate);
