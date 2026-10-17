#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Results;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Jwks;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the
/// <c>JSON Web Key Set (JWKS)</c> endpoint. This endpoint publishes the tenant's asymmetric
/// public keys so that relying parties can validate the signatures of (and encrypt payloads to)
/// tokens issued by this authorization server.
/// </summary>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7517">RFC 7517 - JSON Web Key (JWK)</seealso>
internal class DefaultJwksEndpointHandler(ISecretKeyTypeCatalog secretKeyTypeCatalog)
    : IEndpointProvider
{
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(OpenIdConstants.EndpointPaths.Jwks, HandleRouteAsync)
            .WithName(OpenIdConstants.EndpointNames.Jwks)
            .WithTags(OpenIdConstants.EndpointTags.OpenId)
            .Produces<JsonWebKeySetResult>(StatusCodes.Status200OK)
            .WithOpenIdDiscoverable();

    /// <summary>
    /// JSON Web Key Set (JWKS).
    /// </summary>
    /// <remarks>
    /// Returns the tenant's public signing keys as a JSON Web Key Set (RFC 7517). Clients and resource servers use
    /// these keys to verify the signatures of issued ID tokens and JWT access tokens.
    /// </remarks>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The tenant's public signing keys as a JSON Web Key Set.</response>
    internal async ValueTask<JsonHttpResult<JsonWebKeySetResult>> HandleRouteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();

        var openIdEnvironment = openIdContext.Environment;
        var secretKeys = openIdContext.Tenant.SecretsProvider.Collection;

        // Each key's descriptor (the single extensibility seam, ADR-0104) produces its public JWK; keys with no
        // exportable public material (for example symmetric keys) are omitted.
        var keys = new List<JsonObject>();
        foreach (var secretKey in secretKeys)
        {
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

        var result = new JsonWebKeySetResult { Keys = keys };

        return TypedResults.Json(result, openIdEnvironment.JsonSerializerOptions);
    }
}
