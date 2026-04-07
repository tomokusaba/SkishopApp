using Frontend.Models;

namespace Frontend.Services.Interfaces;

/// <summary>
/// API Gateway クライアントインターフェース
/// BFF パターン: サーバーサイドから API Gateway に通信
/// §4.4 準拠
/// </summary>
public interface IApiGatewayClient
{
    Task<T?> GetAsync<T>(string path, CancellationToken ct = default);
    Task<PaginatedResult<T>> GetPaginatedAsync<T>(string path, int page = 0, int size = 20, string? sort = null, CancellationToken ct = default);
    Task<TResponse?> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken ct = default);
    Task<TResponse?> PutAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
    Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content = null, CancellationToken ct = default);
}
