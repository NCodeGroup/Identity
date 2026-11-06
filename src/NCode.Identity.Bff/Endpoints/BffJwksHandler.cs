// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.AspNetCore;
using NCode.Buffers;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Jwk;
using NCode.Identity.OpenId.Client;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Bff.Endpoints;

/// <summary>
/// Handles <c>GET /bff/jwks</c>, which publishes the public keys of the request's frontend's client-authentication key
/// ring as a JSON Web Key Set (RFC 7517 §5), so the OpenID Provider reads them from the client's registered
/// <c>jwks_uri</c>.
/// </summary>
internal class BffJwksHandler(
    IClientKeyRingProvider keyRingProvider,
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    BffFrontendCatalog frontends
)
{
    private const string JsonContentType = "application/json; charset=utf-8";

    private readonly IClientKeyRingProvider _keyRingProvider = keyRingProvider;
    private readonly ISecretKeyTypeCatalog _secretKeyTypeCatalog = secretKeyTypeCatalog;
    private readonly BffFrontendCatalog _frontends = frontends;

    /// <summary>
    /// Writes the public keys of the client-authentication key ring.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <exception cref="InvalidOperationException">No client-authentication key ring is configured.</exception>
    public virtual Task HandleAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var frontend = _frontends.GetRequired(httpContext);
        var keyRing = frontend.Options.ClientAuthentication.KeyRing;
        if (string.IsNullOrWhiteSpace(keyRing))
            throw new InvalidOperationException(
                $"Bff:Frontends:{frontend.Name}:ClientAuthentication:KeyRing must be set to publish the BFF's keys."
            );

        // Each key's descriptor exports its public JWK; a key with no public part, such as a shared secret, is omitted.
        var keys = new List<JsonWebKey>();
        foreach (var secretKey in _keyRingProvider.GetKeyRing(keyRing))
        {
            if (
                _secretKeyTypeCatalog.TryGet(secretKey.KeyType, out var descriptor)
                && descriptor.ExportPublicKey(secretKey) is { } exported
            )
                keys.Add(exported.Jwk);
        }

        var json = new JsonWebKeySet(keys).ToJsonString();

        // No cache may hold a key set that a rotation has replaced.
        var response = httpContext.Response;
        response.SetNoStore();
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = JsonContentType;
        return response.WriteAsync(json, SecureEncoding.UTF8, httpContext.RequestAborted);
    }
}
