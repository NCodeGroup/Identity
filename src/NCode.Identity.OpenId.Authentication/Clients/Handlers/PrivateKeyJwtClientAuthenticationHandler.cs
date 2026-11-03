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
/// Provides an implementation of <see cref="IClientAuthenticationHandler"/> for the <c>private_key_jwt</c> client
/// authentication method (OpenID Connect Core §9,
/// <see href="https://datatracker.ietf.org/doc/html/rfc7523">RFC 7523</see>): the client presents a
/// <c>client_assertion</c> JWT signed with its private key, verified against the client's registered asymmetric keys.
/// </summary>
internal sealed class PrivateKeyJwtClientAuthenticationHandler(
    IOpenIdClientResolver clientResolver,
    IOpenIdClientFactory clientFactory,
    IAuditEventRecorder auditEventRecorder,
    IClientAuthenticationAttemptLimiter attemptLimiter,
    ILogger<PrivateKeyJwtClientAuthenticationHandler> logger,
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
        attemptLimiter,
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
        OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt;

    /// <inheritdoc />
    protected override bool OwnsAlgorithm(SignatureAlgorithm algorithm) =>
        algorithm.AcceptsOnly<AsymmetricSecretKey>();
}
