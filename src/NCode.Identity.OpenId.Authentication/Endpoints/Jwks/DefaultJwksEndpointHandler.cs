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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Authentication.Contexts;
using NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Converters;
using NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Results;
using NCode.Identity.Secrets.Keys;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Jwks;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the
/// <c>JSON Web Key Set (JWKS)</c> endpoint. This endpoint publishes the tenant's asymmetric
/// public keys so that relying parties can validate the signatures of (and encrypt payloads to)
/// tokens issued by this authorization server.
/// </summary>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7517">RFC 7517 - JSON Web Key (JWK)</seealso>
public class DefaultJwksEndpointHandler(
    IOpenIdContextFactory contextFactory,
    IEnumerable<IJsonWebKeyConverter> jsonWebKeyConverters
) : IEndpointProvider
{
    private IOpenIdContextFactory ContextFactory { get; } = contextFactory;
    private IEnumerable<IJsonWebKeyConverter> JsonWebKeyConverters { get; } = jsonWebKeyConverters;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(OpenIdConstants.EndpointPaths.Jwks, HandleRouteAsync)
            .WithName(OpenIdConstants.EndpointNames.Jwks)
            .WithTags("oidc") // TODO: use constant
            .WithOpenIdDiscoverable();

    private async ValueTask<JsonHttpResult<JsonWebKeySetResult>> HandleRouteAsync(
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = await ContextFactory.CreateAsync(
            httpContext,
            mediator,
            cancellationToken
        );

        var openIdEnvironment = openIdContext.Environment;
        var secretKeys = openIdContext.Tenant.SecretsProvider.Collection;

        var keys = new List<JsonWebKey>();
        foreach (var secretKey in secretKeys)
        {
            var jsonWebKey = ConvertSecretKey(secretKey);
            if (jsonWebKey is not null)
            {
                keys.Add(jsonWebKey);
            }
        }

        var result = new JsonWebKeySetResult { Keys = keys };

        return TypedResults.Json(result, openIdEnvironment.JsonSerializerOptions);
    }

    private JsonWebKey? ConvertSecretKey(SecretKey secretKey)
    {
        // Ask each registered converter in turn; the first that handles the key wins. New key types are
        // supported by registering an additional IJsonWebKeyConverter, without modifying this handler.
        foreach (var converter in JsonWebKeyConverters)
        {
            if (converter.TryConvert(secretKey, out var jsonWebKey))
            {
                return jsonWebKey;
            }
        }

        return null;
    }
}
