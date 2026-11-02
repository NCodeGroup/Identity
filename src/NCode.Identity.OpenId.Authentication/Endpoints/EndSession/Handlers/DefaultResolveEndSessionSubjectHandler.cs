// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Authentication.Settings;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Contexts;
using NCode.Json;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers;

/// <summary>
/// Provides a default implementation of the <see cref="ResolveEndSessionSubjectCommand"/> handler that identifies the
/// session being logged out from the current OP login cookie, falling back to the request's <c>id_token_hint</c>
/// (validated as an ID token the tenant issued, except for its lifetime) when no login is present, and resolves the
/// participating relying parties for back-channel notification. The stable subject (the internal <c>PrincipalId</c>
/// used to key grants and re-projected per-client for
/// back-channel logout) is recovered from server-owned state — the persisted session grant, falling back to the login
/// cookie. The <c>sub</c> carried by the <c>id_token_hint</c> is deliberately never used as the subject identifier:
/// for a <c>pairwise</c> client it is a per-sector pseudonym produced by a one-way projection
/// (<see cref="NCode.Identity.OpenId.Authentication.Subject.ISubjectIdentifierService"/>) the OP cannot and
/// must not reverse. Resolving the subject forward from the <c>sid</c> mirrors the one-way-hashed grant-key model where
/// a presented value is matched forward, never reversed. It also reports whether the end-user must confirm the logout.
/// </summary>
// See ADR-1093, ADR-1096 and ADR-1380.
internal class DefaultResolveEndSessionSubjectHandler(
    IIssuedTokenValidator issuedTokenValidator,
    IOpenIdClientResolver clientResolver,
    ISessionService sessionService,
    ILogger<DefaultResolveEndSessionSubjectHandler> logger
) : ICommandResponseHandler<ResolveEndSessionSubjectCommand, EndSessionSubject>
{
    private IIssuedTokenValidator IssuedTokenValidator { get; } = issuedTokenValidator;
    private IOpenIdClientResolver ClientResolver { get; } = clientResolver;
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
        string? loginSessionId = null;
        HintClaims? hint = null;

        // The id_token_hint identifies the session (sid) and the requesting client (azp/aud). Its `sub` is intentionally
        // not read: for a pairwise client it is a per-sector pseudonym the OP cannot reverse to the stable subject.
        if (!string.IsNullOrEmpty(request.IdTokenHint))
        {
            hint = await TryReadIdTokenHintAsync(
                openIdContext,
                request.IdTokenHint,
                cancellationToken
            );
            if (hint is null)
            {
                Logger.EndSessionIdTokenHintInvalid();
            }
        }

        var clientId = request.ClientId ?? hint?.ClientId;

        // The OP login is read from, and later cleared from, the scheme the authorize endpoint authenticates it with:
        // the client's authorization_authenticate_scheme when the logout names a registered client, otherwise the
        // tenant's, so a host whose default scheme is not the OP login scheme still sees the login.
        var loginScheme = await GetLoginSchemeAsync(openIdContext, clientId, cancellationToken);

        // The current OP login cookie supplies the session (and a provisional subject). The cookie's `sub` is the
        // stable PrincipalId (ADR-1094).
        var authenticateResult = await httpContext.AuthenticateAsync(loginScheme);
        if (authenticateResult.Succeeded)
        {
            var principal = authenticateResult.Principal;
            subjectId = openIdContext.GetSubjectId(principal);
            loginSessionId = principal.FindFirst(JoseClaimNames.Payload.Sid)?.Value;
        }

        // See ADR-1380.
        // The login's session takes precedence, so a confirmed logout ends the session the end-user is signed in to;
        // the hint's sid identifies the session only when no login is present.
        var sessionId = string.IsNullOrEmpty(loginSessionId) ? hint?.SessionId : loginSessionId;

        // See ADR-1380.
        // RP-Initiated Logout 1.0 section 2: the end-user must confirm unless a valid hint names the current session.
        var requiresConfirmation =
            authenticateResult.Succeeded
            && (
                string.IsNullOrEmpty(hint?.SessionId)
                || !string.Equals(hint?.SessionId, loginSessionId, StringComparison.Ordinal)
            );

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

        return new EndSessionSubject(
            subjectId,
            sessionId,
            clientId,
            participatingClientIds,
            requiresConfirmation,
            loginScheme
        );
    }

    /// <summary>
    /// Gets the scheme the authorize endpoint authenticates the end-user's OP login with for this logout: the
    /// <c>authorization_authenticate_scheme</c> of the client the logout names when it is registered, because a client
    /// may override the setting, otherwise the tenant's.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="clientId">The client the logout names, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may cancel the operation.</param>
    /// <returns>The scheme's name, or <see langword="null"/> for the host's default scheme.</returns>
    private async ValueTask<string?> GetLoginSchemeAsync(
        OpenIdContext openIdContext,
        string? clientId,
        CancellationToken cancellationToken
    )
    {
        var client = string.IsNullOrEmpty(clientId)
            ? null
            : await ClientResolver.ResolveClientOrDefaultAsync(
                openIdContext,
                clientId,
                cancellationToken
            );

        var settings = client?.Settings ?? openIdContext.Tenant.SettingsProvider.Collection;
        return settings.GetLoginScheme();
    }

    private async ValueTask<HintClaims?> TryReadIdTokenHintAsync(
        OpenIdContext openIdContext,
        string idTokenHint,
        CancellationToken cancellationToken
    )
    {
        // The id_token_hint is typically expired at logout time, so its lifetime is not validated; every other check of
        // an ID token is, so only tokens this OP issued are trusted to identify the session.
        var result = await IssuedTokenValidator.ValidateIdTokenHintAsync(
            openIdContext,
            idTokenHint,
            cancellationToken
        );

        if (!result.IsValid)
        {
            // A hint that cannot be parsed or validated simply does not identify a session.
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
