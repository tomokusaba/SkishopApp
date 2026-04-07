namespace PointService.Events;

public record PointsEarnedEvent(
    string UserId, int Points, string OrderId,
    int NewBalance, string CorrelationId, DateTime OccurredAt);

public record PointsRedeemedEvent(
    string UserId, int Points, string OrderId,
    int NewBalance, string CorrelationId, DateTime OccurredAt);

public record PointsReservedEvent(
    string UserId, int Points, string OrderId,
    string CorrelationId, DateTime OccurredAt);

public record PointsReleasedEvent(
    string UserId, int Points, string OrderId,
    string CorrelationId, DateTime OccurredAt);

public record PointsExpiredEvent(
    string UserId, int Points,
    int NewBalance, string CorrelationId, DateTime OccurredAt);

public record MemberRankUpdatedEvent(
    string UserId, string CurrentRank, decimal PointRate, DateTime OccurredAt);

public record UserRankCache(string Rank, decimal PointRate);
