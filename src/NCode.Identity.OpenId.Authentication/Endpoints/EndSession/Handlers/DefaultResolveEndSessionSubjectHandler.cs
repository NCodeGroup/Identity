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
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers;

/// <summary>
/// Provides a default implementation of the <see cref="ResolveEndSessionSubjectCommand"/> handler that identifies the
/// subject and session being logged out from the request's <c>id_token_hint</c> (whose signature and issuer are
/// validated, but not its lifetime), falling back to the current OP login cookie, and resolves the participating
/// relying parties for back-channel notification.
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

    private readonly record struct HintClaims(string? Subject, string? SessionId, string? ClientId);

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

        // The id_token_hint identifies the subject/session and the requesting client.
        if (!string.IsNullOrEmpty(request.IdTokenHint))
        {
            var hint = await TryReadIdTokenHintAsync(
                openIdContext,
                request.IdTokenHint,
                cancellationToken
            );
            if (hint is { } claims)
            {
                subjectId = claims.Subject;
                sessionId = claims.SessionId;
                clientId ??= claims.ClientId;
            }
            else
            {
                Logger.EndSessionIdTokenHintInvalid();
            }
        }

        // Fall back to the current cookie session when no usable id_token_hint was supplied.
        if (string.IsNullOrEmpty(subjectId) || string.IsNullOrEmpty(sessionId))
        {
            var authenticateResult = await httpContext.AuthenticateAsync();
            if (authenticateResult.Succeeded)
            {
                var principal = authenticateResult.Principal;
                subjectId ??= openIdContext.GetSubjectId(principal);
                sessionId ??= principal.FindFirst(JoseClaimNames.Payload.Sid)?.Value;
            }
        }

        if (string.IsNullOrEmpty(subjectId) && string.IsNullOrEmpty(sessionId))
        {
            Logger.EndSessionSubjectUnresolved();
        }

        // Resolve the participating relying parties (back-channel recipients) before the session is revoked.
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
                participatingClientIds = persistedSession.Payload.ClientIds;
            }
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
                GetString(payload, JoseClaimNames.Payload.Sub),
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
        payload.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

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
