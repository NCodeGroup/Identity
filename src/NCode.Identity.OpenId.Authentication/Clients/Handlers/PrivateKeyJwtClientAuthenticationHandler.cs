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

using Microsoft.Extensions.Logging;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.JsonWebTokens;
using NCode.Identity.OpenId.Authentication.Auditing;
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
    ILogger<PrivateKeyJwtClientAuthenticationHandler> logger,
    IJsonWebTokenService jsonWebTokenService,
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    IClientAssertionReplayCache replayCache,
    IClientKeyResolver clientKeyResolver
)
    : ClientAssertionClientAuthenticationHandler(
        clientResolver,
        clientFactory,
        auditEventRecorder,
        logger,
        jsonWebTokenService,
        algorithmCollectionProvider,
        replayCache,
        clientKeyResolver
    )
{
    /// <inheritdoc />
    public override string AuthenticationMethod =>
        OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt;

    /// <inheritdoc />
    protected override bool OwnsAlgorithm(SignatureAlgorithm algorithm) =>
        algorithm.KeyType != typeof(SymmetricSecretKey);
}
