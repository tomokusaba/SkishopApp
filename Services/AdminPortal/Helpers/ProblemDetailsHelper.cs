using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AdminPortal.Helpers;

/// <summary>
/// RFC 9457 ProblemDetails パース基盤
/// H-23: API エラーレスポンスからユーザー向けメッセージを抽出
/// </summary>
public static class ProblemDetailsHelper
{
    public static async Task<string> ExtractErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken ct = default)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(ct);
            if (problem is not null)
            {
                if (problem.Errors is { Count: > 0 })
                {
                    var fieldErrors = problem.Errors
                        .SelectMany(e => e.Value.Select(v => $"{e.Key}: {v}"));
                    return string.Join("; ", fieldErrors);
                }
                return problem.Detail ?? problem.Title ?? $"エラーが発生しました (HTTP {(int)response.StatusCode})";
            }
        }
        catch
        {
            // JSON パース失敗時はフォールバック
        }

        return $"エラーが発生しました (HTTP {(int)response.StatusCode})";
    }
}

public record ProblemDetailsDto
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("status")]
    public int? Status { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("instance")]
    public string? Instance { get; init; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }

    [JsonPropertyName("errors")]
    public Dictionary<string, string[]>? Errors { get; init; }
}
