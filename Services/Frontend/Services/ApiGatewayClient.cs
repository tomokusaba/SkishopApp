using System.Net.Http.Json;
using Frontend.Models;
using Frontend.Services.Interfaces;

namespace Frontend.Services;

/// <summary>
/// BFF パターン API クライアント基盤
/// §4.4 準拠 — IHttpClientFactory 経由で API Gateway に通信
/// </summary>
public class ApiGatewayClient(
    HttpClient httpClient,
    IApiErrorHandler errorHandler,
    ILogger<ApiGatewayClient> logger) : IApiGatewayClient
{
    public async Task<T?> GetAsync<T>(string path, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync(path, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(ct);
    }

    public async Task<PaginatedResult<T>> GetPaginatedAsync<T>(
        string path, int page = 0, int size = 20, string? sort = null, CancellationToken ct = default)
    {
        var queryParams = new Dictionary<string, string?>
        {
            ["page"] = page.ToString(),
            ["size"] = size.ToString(),
            ["sort"] = sort
        };
        var queryString = string.Join("&",
            queryParams.Where(kv => kv.Value is not null).Select(kv => $"{kv.Key}={kv.Value}"));

        var fullPath = string.IsNullOrEmpty(queryString) ? path : $"{path}?{queryString}";
        var response = await httpClient.GetAsync(fullPath, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<PaginatedResult<T>>(ct)
            ?? new PaginatedResult<T>([], 0, page, size);
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string path, TRequest body, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(path, body, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TResponse>(ct);
    }

    public async Task<TResponse?> PutAsync<TRequest, TResponse>(
        string path, TRequest body, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync(path, body, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TResponse>(ct);
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync(path, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, HttpContent? content = null, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        var response = await httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return response;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("API エラー: {StatusCode} {Path}",
                (int)response.StatusCode, response.RequestMessage?.RequestUri);
            await errorHandler.HandleApiErrorAsync(response, ct);
        }
    }
}
