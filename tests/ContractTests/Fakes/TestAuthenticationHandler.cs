using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KartInventoryService.ContractTests.Fakes;

/// <summary>
/// Replaces real JWT/JWKS validation in the contract-test host - always authenticates
/// successfully, with `roles` claims driven by a test-only `X-Test-Roles` header (comma
/// separated), so tests can exercise OrderServicePolicy/ReplenishPolicy's RBAC gating without a
/// real kart-identity-service token. Mirrors kart-category-service's ContractTests convention.
/// </summary>
public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    private const string RolesHeader = "X-Test-Roles";

    public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, "test-principal") };

        if (Request.Headers.TryGetValue(RolesHeader, out var rolesHeader))
        {
            claims.AddRange(rolesHeader.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(role => new Claim("roles", role)));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
