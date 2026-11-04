// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Jwk;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Jwks;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the
/// <c>JSON Web Key Set (JWKS)</c> endpoint. This endpoint publishes the tenant's asymmetric
/// public keys so that relying parties can validate the signatures of (and encrypt payloads to)
/// tokens issued by this authorization server. Every key in its validity window is published, the upcoming and the
/// retired ones included, so a verifier holds a key before the first token it signs and after the last; an expired
/// key is not.
/// </summary>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7517">RFC 7517 - JSON Web Key (JWK)</seealso>
// See ADR-1403.
internal class DefaultJwksEndpointHandler(
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    TimeProvider timeProvider
) : IEndpointProvider
{
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;
    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(OpenIdConstants.EndpointPaths.Jwks, HandleRouteAsync)
            .WithName(OpenIdConstants.EndpointNames.Jwks)
            .WithTags(OpenIdConstants.EndpointTags.OpenId)
            .Produces<JsonWebKeySet>(StatusCodes.Status200OK)
            .WithOpenIdDiscoverable()
            // Public metadata any anonymous caller may fetch is not the tenant's metered usage (ADR-1198).
            .WithOpenIdMetered(false);

    /// <summary>
    /// JSON Web Key Set (JWKS).
    /// </summary>
    /// <remarks>
    /// Returns the tenant's public signing keys as a JSON Web Key Set (RFC 7517). Clients and resource servers use
    /// these keys to verify the signatures of issued ID tokens and JWT access tokens. A key is published from its
    /// creation until it expires, so the set holds a key before it starts to sign and after it stops.
    /// </remarks>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The tenant's public signing keys as a JSON Web Key Set.</response>
    internal async ValueTask<JsonHttpResult<JsonWebKeySet>> HandleRouteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();

        var openIdEnvironment = openIdContext.Environment;
        var secretKeys = openIdContext.Tenant.SecretsProvider.Collection;

        // Each key's descriptor (the single extensibility seam, ADR-1104) produces its public JWK; keys with no
        // exportable public material (for example symmetric keys) are omitted, and so is an expired key, which
        // verifies nothing a relying party could still accept (ADR-1403).
        var now = TimeProvider.GetUtcNow();
        var keys = new List<JsonWebKey>();
        foreach (var secretKey in secretKeys)
        {
            if (secretKey.Metadata.IsExpired(now))
            {
                continue;
            }

            if (!SecretKeyTypeCatalog.TryGet(secretKey.KeyType, out var descriptor))
            {
                continue;
            }

            var exported = descriptor.ExportPublicKey(secretKey);
            if (exported is not null)
            {
                keys.Add(exported.Jwk);
            }
        }

        var result = new JsonWebKeySet(keys);

        return TypedResults.Json(result, openIdEnvironment.JsonSerializerOptions);
    }
}
