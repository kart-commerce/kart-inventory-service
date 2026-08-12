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
/// Inventory & Stock Management flow fix (2026-08-12): `service:order` was never actually
/// issuable by kart-identity-service - its ServicePrincipal.Provision only allows the `Admin` or
/// `PartnerApi` PlatformRole values, and no `order-service` client was ever seeded, so this
/// policy was unreachable via any real Identity-issued token (found live during flow #7, not
/// fixed there - see [[kart-flow7-order-management-admin-done]]). Fixed here by widening
/// OrderServicePolicy to also accept `partner_api` (dual-claim, exactly like ReplenishPolicy's own
/// admin-or-trigger precedent below) and seeding a real `order-service` PartnerApi principal in
/// kart-devops's global.json - the smallest change that makes this policy actually satisfiable
/// today without touching Identity's role vocabulary/domain invariants. `service:order` is kept
/// as the first accepted value for documentation/contract-clarity and in case a future session
/// does widen Identity's vocabulary.
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

    /// <summary>Order Management (Admin) flow #7's read-only "Assign Warehouse" view (GET /v1/inventory/orders/{orderId}/allocations), called by kart-admin-service's own client-credentials principal — same shape as OrderServicePolicy but for Admin's `admin` role rather than order-service's `service:order` one.</summary>
    public const string AdminOnlyPolicy = "AdminOnly";

    private const string RolesClaimType = "roles";
    private const string OrderServiceRoleValue = "service:order";
    private const string AdminRoleValue = "admin";
    private const string ReplenishTriggerRoleValue = "service:inventory-replenishment-trigger";

    /// <summary>The role value kart-identity-service can actually issue for a non-interactive client that isn't Admin (PlatformRole.PartnerApi -> "partner_api") - see the OrderServicePolicy fix note above.</summary>
    private const string PartnerApiRoleValue = "partner_api";

    public static IServiceCollection AddInventoryAuthentication(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddHttpClient<JwksSigningKeyResolver>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwksSigningKeyResolver>((options, resolver) =>
            {
                // Defensively disable .NET's default inbound claim-type remapping so the raw
                // "roles" claim name survives verbatim (every policy below matches on it
                // literally) - the same claim-remapping gap closed in kart-category-service and
                // kart-order-service.
                options.MapInboundClaims = false;

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
            .AddPolicy(OrderServicePolicy, policy => policy.RequireClaim(RolesClaimType, OrderServiceRoleValue, PartnerApiRoleValue))
            .AddPolicy(ReplenishPolicy, policy => policy.RequireClaim(RolesClaimType, AdminRoleValue, ReplenishTriggerRoleValue))
            .AddPolicy(AdminOnlyPolicy, policy => policy.RequireClaim(RolesClaimType, AdminRoleValue));

        return services;
    }
}
