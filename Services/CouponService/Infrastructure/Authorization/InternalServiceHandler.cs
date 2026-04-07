using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace CouponService.Infrastructure.Authorization;

public class InternalServiceRequirement : IAuthorizationRequirement { }

public class InternalServiceHandler : AuthorizationHandler<InternalServiceRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        InternalServiceRequirement requirement)
    {
        // sub claim may be mapped to ClaimTypes.NameIdentifier by JwtBearerHandler
        var hasSub = context.User.HasClaim(c =>
            (c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier) && c.Value == "internal-service");
        var hasScope = context.User.HasClaim(c =>
            c.Type == "scope" && c.Value.Contains("coupon:manage"));
        // iss claim may be mapped by JwtBearerHandler
        var hasIssuer = context.User.HasClaim(c =>
            (c.Type == "iss" || c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/uri")
            && !string.IsNullOrEmpty(c.Value));

        if (hasSub && hasScope && hasIssuer)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
