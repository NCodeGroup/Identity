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

using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using NCode.Identity.Jose;
using NCode.Identity.JsonWebTokens;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Json;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers;

/// <summary>
/// Provides a default implementation of the <see cref="ResolveEndSessionSubjectCommand"/> handler that identifies the
/// session being logged out from the request's <c>id_token_hint</c> (whose signature and issuer are validated, but not
/// its lifetime) or the current OP login cookie, and resolves the participating relying parties for back-channel
/// notification. The stable subject (the internal <c>PrincipalId</c> used to key grants and re-projected per-client for
/// back-channel logout) is recovered from server-owned state — the persisted session grant, falling back to the login
/// cookie. The <c>sub</c> carried by the <c>id_token_hint</c> is deliberately never used as the subject identifier:
/// for a <c>pairwise</c> client it is a per-sector pseudonym produced by a one-way projection
/// (<see cref="NCode.Identity.OpenId.Authentication.Subject.ISubjectIdentifierService"/>, ADR-0093) the OP cannot and
/// must not reverse. Resolving the subject forward from the <c>sid</c> mirrors the one-way-hashed grant-key model where
/// a presented value is matched forward, never reversed (ADR-0096).
/// </summary>
internal class DefaultResolveEndSessionSubjectHandler(
    IJsonWebTokenService jsonWebTokenService,
    ISessionService sessionService,
    ILogger<DefaultResolveEndSessionSubjectHandler> logger
) : ICommandResponseHandler<ResolveEndSessionSubjectCommand, EndSessionSubject>
{
    private IJsonWebTokenService JsonWebTokenService { get; } = jsonWebTokenService;
    private ISessionService SessionService { get; } = sessionService;
    private ILogger<DefaultResolveEndSessionSubjectHandler> Logger { get; } = logger;

    private readonly record struct HintClaims(string? SessionId, string? ClientId);

    /// <inheritdoc />
    public async ValueTask<EndSessionSubject> HandleAsync(
        ResolveEndSessionSubjectCommand command,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = command.OpenIdContext;
        var request = command.Request;
        var httpContext = openIdContext.Http;

        string? subjectId = null;
        string? sessionId = null;
        var clientId = request.ClientId;

        // The id_token_hint identifies the session (sid) and the requesting client (azp/aud). Its `sub` is intentionally
        // not read: for a pairwise client it is a per-sector pseudonym the OP cannot reverse to the stable subject.
        if (!string.IsNullOrEmpty(request.IdTokenHint))
        {
            var hint = await TryReadIdTokenHintAsync(
                openIdContext,
                request.IdTokenHint,
                cancellationToken
            );
            if (hint is { } claims)
            {
                sessionId = claims.SessionId;
                clientId ??= claims.ClientId;
            }
            else
            {
                Logger.EndSessionIdTokenHintInvalid();
            }
        }

        // Fall back to the current OP login cookie for the session (and a provisional subject) when the hint did not
        // supply a sid. The cookie's `sub` is the stable PrincipalId (ADR-0094).
        if (string.IsNullOrEmpty(sessionId))
        {
            var authenticateResult = await httpContext.AuthenticateAsync();
            if (authenticateResult.Succeeded)
            {
                var principal = authenticateResult.Principal;
                subjectId = openIdContext.GetSubjectId(principal);
                sessionId = principal.FindFirst(JoseClaimNames.Payload.Sid)?.Value;
            }
        }

        // The persisted session grant is the authoritative source of the stable subject (PrincipalId) and of the
        // participating relying parties. Resolving it forward from the sid (never reversing a pairwise `sub`) recovers
        // the stable subject that keys grants and is re-projected per-client for back-channel logout.
        IReadOnlyList<string> participatingClientIds = [];
        if (!string.IsNullOrEmpty(sessionId))
        {
            var session = await SessionService.GetOrDefaultAsync(
                openIdContext,
                sessionId,
                cancellationToken
            );
            if (session is { } persistedSession)
            {
                subjectId = persistedSession.SubjectId ?? subjectId;
                participatingClientIds = persistedSession.Payload.ClientIds;
            }
        }

        if (string.IsNullOrEmpty(subjectId) && string.IsNullOrEmpty(sessionId))
        {
            Logger.EndSessionSubjectUnresolved();
        }

        return new EndSessionSubject(subjectId, sessionId, clientId, participatingClientIds);
    }

    private async ValueTask<HintClaims?> TryReadIdTokenHintAsync(
        OpenIdContext openIdContext,
        string idTokenHint,
        CancellationToken cancellationToken
    )
    {
        var openIdTenant = openIdContext.Tenant;

        try
        {
            // The id_token_hint is typically expired at logout time, so its lifetime is not validated; the signature
            // and issuer are, so only tokens this OP issued are trusted to identify the session.
            var parameters = new ValidateJwtParameters()
                .UseValidationKeys(openIdTenant.SecretsProvider.Collection)
                .ValidateIssuer(openIdTenant.Issuer);

            var result = await JsonWebTokenService.ValidateJwtAsync(
                idTokenHint,
                parameters,
                cancellationToken
            );

            if (!result.IsValid)
            {
                return null;
            }

            var payload = result.DecodedJwt.Payload;
            return new HintClaims(
                GetString(payload, JoseClaimNames.Payload.Sid),
                GetString(payload, JoseClaimNames.Payload.Azp)
                    ?? GetString(payload, JoseClaimNames.Payload.ClientId)
                    ?? GetAudience(payload)
            );
        }
        catch
        {
            // A hint that cannot be parsed or validated simply does not identify a session.
            return null;
        }
    }

    private static string? GetString(JsonElement payload, string name) =>
        payload.TryGetPropertyValue<string>(name, out var value) ? value : null;

    private static string? GetAudience(JsonElement payload)
    {
        if (!payload.TryGetProperty(JoseClaimNames.Payload.Aud, out var audience))
        {
            return null;
        }

        return audience.ValueKind switch
        {
            JsonValueKind.String => audience.GetString(),
            JsonValueKind.Array when audience.GetArrayLength() > 0 => audience[0].GetString(),
            _ => null,
        };
    }
}
