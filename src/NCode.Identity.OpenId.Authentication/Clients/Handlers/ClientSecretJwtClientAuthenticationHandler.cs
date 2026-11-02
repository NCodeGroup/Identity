// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Authentication.Clients.Handlers;

/// <summary>
/// Provides an implementation of <see cref="IClientAuthenticationHandler"/> for the <c>client_secret_jwt</c> client
/// authentication method (OpenID Connect Core §9): the client presents a <c>client_assertion</c> JWT signed with an
/// HMAC keyed by the client secret, verified against the client's registered symmetric key. Symmetric client
/// authentication is permitted by OpenID Connect but is excluded by the FAPI security profiles, which require
/// asymmetric (<c>private_key_jwt</c>) or mutual-TLS client authentication.
/// </summary>
internal sealed class ClientSecretJwtClientAuthenticationHandler(
    IOpenIdClientResolver clientResolver,
    IOpenIdClientFactory clientFactory,
    IAuditEventRecorder auditEventRecorder,
    ILogger<ClientSecretJwtClientAuthenticationHandler> logger,
    IJwtValidator jwtValidator,
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    IClientAssertionReplayCache replayCache,
    IClientKeyResolver clientKeyResolver,
    ISecurityProfileCatalog securityProfileCatalog
)
    : ClientAssertionClientAuthenticationHandler(
        clientResolver,
        clientFactory,
        auditEventRecorder,
        logger,
        jwtValidator,
        algorithmCollectionProvider,
        replayCache,
        clientKeyResolver,
        securityProfileCatalog
    )
{
    /// <inheritdoc />
    public override string AuthenticationMethod =>
        OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt;

    /// <inheritdoc />
    protected override bool OwnsAlgorithm(SignatureAlgorithm algorithm) =>
        algorithm.AcceptsOnly<SymmetricSecretKey>();
}
