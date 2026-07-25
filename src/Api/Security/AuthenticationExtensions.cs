using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace KartInventoryService.Api.Security;

/// <summary>
/// contracts/api-contract.yaml's `clientCredentials` security scheme: an Identity-issued RS256
/// JWT, checked structurally here (signature + expiry), never re-deriving role grants locally.
///
/// requirement-spec.md S24.1.2: CanWrite on POST /inventory/reserve and POST /inventory/release
/// is gated by an inline check that the caller is Order Service's own client-credentials
/// principal - "the same narrow, per-endpoint, service-principal-only mechanism
/// kart-identity-service's internal lock/unlock endpoints ... already use" - OrderServicePolicy
/// requires the `roles` claim (kart-identity-service's JwtAccessTokenGenerator claim shape,
/// mirrored from kart-category-service's own AdminPolicy) to carry `service:order`.
///
/// POST /inventory/replenish (this repo's Implementation Addendum - see contracts/README.md)
/// accepts either Admin's own operator principal or the internal automated-reorder-trigger
/// principal (requirement-spec.md Decision 5) - ReplenishPolicy allows either `roles` value.
///
/// GET /inventory/{sku} stays unauthenticated - CanRead is unconditional (ddd-model.md).
/// </summary>
public static class AuthenticationExtensions
{
    public const string OrderServicePolicy = "OrderServiceOnly";
    public const string ReplenishPolicy = "ReplenishAuthorized";

    private const string RolesClaimType = "roles";
    private const string OrderServiceRoleValue = "service:order";
    private const string AdminRoleValue = "admin";
    private const string ReplenishTriggerRoleValue = "service:inventory-replenishment-trigger";

    public static IServiceCollection AddInventoryAuthentication(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddHttpClient<JwksSigningKeyResolver>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwksSigningKeyResolver>((options, resolver) =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Identity's JwtAccessTokenGenerator sets neither `iss` nor `aud` on the
                    // tokens it mints - validating either here would reject every real token.
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeyResolver = resolver.ResolveSigningKeys,
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(OrderServicePolicy, policy => policy.RequireClaim(RolesClaimType, OrderServiceRoleValue))
            .AddPolicy(ReplenishPolicy, policy => policy.RequireClaim(RolesClaimType, AdminRoleValue, ReplenishTriggerRoleValue));

        return services;
    }
}
