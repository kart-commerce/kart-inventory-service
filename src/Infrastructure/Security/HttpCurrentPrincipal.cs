using System.IdentityModel.Tokens.Jwt;
using KartInventoryService.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace KartInventoryService.Infrastructure.Security;

/// <summary>
/// Resolves BRD S24.3's acting principal from the caller's Identity-issued access token `sub`
/// claim - Order Service's or Admin's own client-credentials principal, per requirement-spec.md
/// S24.1.2/S24.3 (mirrors kart-category-service's HttpCurrentPrincipal). Background processes
/// (TTL sweep, order-events consumers) never resolve through this - they pass their own
/// well-known `system:*` principal directly instead.
/// </summary>
public sealed class HttpCurrentPrincipal : ICurrentPrincipal
{
    private const string UnknownPrincipal = "system:unknown";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentPrincipal(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string ActingPrincipal =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? UnknownPrincipal;
}
