using Frontend.DTOs;
using Frontend.Services.Interfaces;

namespace Frontend.Services;

/// <summary>
/// AI API クライアント（§9.8 AI API）
/// </summary>
public class AiApiClient(
    IApiGatewayClient apiClient,
    ILogger<AiApiClient> logger) : IAiApiClient
{
    public async Task<List<AiRecommendationDto>> GetTrendingAsync(int count = 8, CancellationToken ct = default)
    {
        try
        {
            return await apiClient.GetAsync<List<AiRecommendationDto>>(
                $"/api/v1/ai/recommendations/trending?count={count}", ct) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI trending API 障害: グレースフルデグラデーション適用");
            return [];
        }
    }

    public async Task<List<AiRecommendationDto>> GetSeasonalAsync(int count = 8, CancellationToken ct = default)
    {
        try
        {
            return await apiClient.GetAsync<List<AiRecommendationDto>>(
                $"/api/v1/ai/recommendations/seasonal?count={count}", ct) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI seasonal API 障害: グレースフルデグラデーション適用");
            return [];
        }
    }

    public async Task<List<AiRecommendationDto>> GetPersonalizedAsync(int count = 8, CancellationToken ct = default)
    {
        try
        {
            return await apiClient.GetAsync<List<AiRecommendationDto>>(
                $"/api/v1/ai/recommendations/personalized?count={count}", ct) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI personalized API 障害: グレースフルデグラデーション適用");
            return [];
        }
    }

    public async Task<List<AiRecommendationDto>> GetSimilarAsync(string productId, int count = 8, CancellationToken ct = default)
    {
        try
        {
            return await apiClient.GetAsync<List<AiRecommendationDto>>(
                $"/api/v1/ai/recommendations/similar/{productId}?count={count}", ct) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI similar API 障害: グレースフルデグラデーション適用 ProductId={ProductId}", productId);
            return [];
        }
    }

    public async Task<List<AiRecommendationDto>> GetFrequentlyBoughtAsync(string productId, int count = 8, CancellationToken ct = default)
    {
        try
        {
            return await apiClient.GetAsync<List<AiRecommendationDto>>(
                $"/api/v1/ai/recommendations/frequently-bought/{productId}?count={count}", ct) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI frequently-bought API 障害: グレースフルデグラデーション適用 ProductId={ProductId}", productId);
            return [];
        }
    }

    public async Task<List<SearchSuggestDto>> GetSearchSuggestionsAsync(string query, CancellationToken ct = default)
    {
        try
        {
            return await apiClient.GetAsync<List<SearchSuggestDto>>(
                $"/api/v1/ai/search/suggest?query={Uri.EscapeDataString(query)}", ct) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI search suggest API 障害: サジェスト非表示");
            return [];
        }
    }

    public async Task PostRecommendationFeedbackAsync(RecommendationFeedbackRequest request, CancellationToken ct = default)
    {
        try
        {
            await apiClient.PostAsync<RecommendationFeedbackRequest, object>(
                "/api/v1/ai/recommendations/feedback", request, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI feedback API 障害: フィードバック送信失敗");
        }
    }

    public async Task<ChatSessionDto?> CreateChatSessionAsync(CancellationToken ct = default)
    {
        try
        {
            return await apiClient.PostAsync<object, ChatSessionDto>("/api/v1/ai/chat/sessions", new { }, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI chat session creation failed");
            return null;
        }
    }

    public async Task EscalateChatAsync(string sessionId, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("AI チャットエスカレーション: SessionId={SessionId}", sessionId);
            await apiClient.PostAsync<object, object>(
                $"/api/v1/ai/chat/sessions/{sessionId}/escalate", new { }, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI チャットエスカレーション失敗: SessionId={SessionId}", sessionId);
        }
    }
}

