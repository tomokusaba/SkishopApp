using Frontend.DTOs;

namespace Frontend.Services.Interfaces;

/// <summary>
/// AI API クライアントインターフェース（§9.8 AI API）
/// </summary>
public interface IAiApiClient
{
    Task<List<AiRecommendationDto>> GetTrendingAsync(int count = 8, CancellationToken ct = default);
    Task<List<AiRecommendationDto>> GetSeasonalAsync(int count = 8, CancellationToken ct = default);
    Task<List<AiRecommendationDto>> GetPersonalizedAsync(int count = 8, CancellationToken ct = default);
    Task<List<AiRecommendationDto>> GetSimilarAsync(string productId, int count = 8, CancellationToken ct = default);
    Task<List<AiRecommendationDto>> GetFrequentlyBoughtAsync(string productId, int count = 8, CancellationToken ct = default);
    Task<List<SearchSuggestDto>> GetSearchSuggestionsAsync(string query, CancellationToken ct = default);
    Task PostRecommendationFeedbackAsync(RecommendationFeedbackRequest request, CancellationToken ct = default);
    Task<ChatSessionDto?> CreateChatSessionAsync(CancellationToken ct = default);
    Task EscalateChatAsync(string sessionId, CancellationToken ct = default);
}
