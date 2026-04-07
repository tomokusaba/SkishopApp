using System.Net.Http.Headers;
using System.Security.Claims;

namespace AdminPortal.Helpers;

/// <summary>
/// HttpClient の送信リクエストに、ログイン済みユーザーの JWT トークンを
/// Authorization Bearer ヘッダーとして自動付与する DelegatingHandler。
/// </summary>
public class JwtForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var jwtToken = httpContext?.User?.FindFirstValue("jwt_token");

        if (!string.IsNullOrEmpty(jwtToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
