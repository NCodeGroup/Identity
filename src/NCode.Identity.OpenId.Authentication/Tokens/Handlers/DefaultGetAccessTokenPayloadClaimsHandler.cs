#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using NCode.Identity.Cryptography;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Tokens.Handlers;

/// <summary>
/// Provides a default implementation for a <see cref="GetAccessTokenPayloadClaimsCommand"/> handler that generates the
/// payload claims for an access token.
/// </summary>
/// <remarks>
/// This handler is responsible for generating protocol claims and any claims derived from the subject are handled by
/// <see cref="DefaultGetAccessTokenSubjectClaimsHandler"/>.
/// </remarks>
internal class DefaultGetAccessTokenPayloadClaimsHandler(ICryptoService cryptoService)
    : ICommandHandler<GetAccessTokenPayloadClaimsCommand>
{
    private ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        GetAccessTokenPayloadClaimsCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, tokenContext, payloadClaims) = command;
        var (tokenRequest, _, _, _) = tokenContext;

        var tenant = openIdContext.Tenant;

        if (!payloadClaims.ContainsKey(JoseClaimNames.Payload.Jti))
        {
            const int byteLength = 16; // aka 128 bits which is larger than the entropy of GUID v4 (122 bits)
            var tokenId = CryptoService.GenerateUrlSafeKey(byteLength);

            payloadClaims[JoseClaimNames.Payload.Jti] = tokenId;
        }

        payloadClaims[JoseClaimNames.Payload.Tid] = tenant.TenantId;
        payloadClaims[JoseClaimNames.Payload.ClientId] = openIdClient.ClientId;

        // scope
        // https://datatracker.ietf.org/doc/html/rfc9068#section-2.2.3
        // The 'offline_access' scope is only meaningful when an end-user (subject) is present since it
        // controls the issuance of a refresh token bound to that user; omit it otherwise.
        var hasSubject = tokenRequest.SubjectAuthentication.HasValue;
        var scopesToInclude = hasSubject
            ? tokenRequest.EffectiveScopes
            : tokenRequest.EffectiveScopes.Where(scope =>
                !string.Equals(
                    scope,
                    OpenIdConstants.ScopeTypes.OfflineAccess,
                    StringComparison.Ordinal
                )
            );

        var scopeValue = string.Join(OpenIdProtocol.ParameterSeparatorChar, scopesToInclude);
        if (!string.IsNullOrEmpty(scopeValue))
        {
            payloadClaims[JoseClaimNames.Payload.Scope] = scopeValue;
        }

        // cnf: bind the access token to the request's proof-of-possession key (DPoP jkt / mutual-TLS x5t#S256)
        // when one was established for the request (RFC 9449 §6, RFC 8705 §3.1).
        if (!payloadClaims.ContainsKey(JoseClaimNames.Payload.Cnf))
        {
            var confirmation = tokenRequest.Confirmation.ToClaimValueOrNull();
            if (confirmation is not null)
            {
                payloadClaims[JoseClaimNames.Payload.Cnf] = confirmation;
            }
        }

        // sid (session identifier, RFC 9068 §2.2.1): always emitted for a subject-bound access token so a resource
        // server can correlate the token to the end-user session and the OP's own endpoints can honor session-scoped
        // revocation — the non-bypassable enabler for authorization-code-replay revocation (ADR-0112).
        var sessionId = tokenRequest.SubjectAuthentication?.SessionId;
        if (
            !string.IsNullOrEmpty(sessionId)
            && !payloadClaims.ContainsKey(JoseClaimNames.Payload.Sid)
        )
        {
            payloadClaims[JoseClaimNames.Payload.Sid] = sessionId;
        }

        return ValueTask.CompletedTask;
    }
}
