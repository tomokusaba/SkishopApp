namespace Frontend.DTOs;

public record AiRecommendationDto(
    string ProductId,
    string ProductName,
    decimal Price,
    string ImageUrl,
    string CategoryName,
    double Score,
    string Reason);

public record SearchSuggestDto(string Text, string Category, string? ProductId);
public record RecommendationFeedbackRequest(string ProductId, string RecommendationType, bool IsPositive);
public record ChatSessionDto(string Id, string UserId, string? Title, string Status, DateTime CreatedAt, DateTime UpdatedAt, DateTime? ClosedAt);
