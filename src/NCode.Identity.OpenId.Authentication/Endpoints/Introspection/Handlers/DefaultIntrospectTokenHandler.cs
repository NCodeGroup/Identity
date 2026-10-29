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
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Results;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Contexts;
using NCode.Json;
using NCode.Mediator;
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="IntrospectTokenCommand"/> message. It resolves
/// whether the presented token is active for the caller and contributes the claims the caller may see. An access token
/// is active only for a caller acting for a resource server the token names in its audience: it is validated through
/// <see cref="IIssuedTokenValidator"/>, decrypted with the tenant's keys and that resource server's own, and its
/// <c>aud</c> and <c>scope</c> are narrowed to that resource server. A refresh token (a persisted grant) is active
/// only for the client it was issued to. Every other token is inactive. Additional handlers may enrich the response.
/// </summary>
// See ADR-0029 and ADR-0168 (RFC 7662 §4, RFC 9701 §5).
internal class DefaultIntrospectTokenHandler(
    IIssuedTokenValidator issuedTokenValidator,
    ISubjectIdentifierService subjectIdentifierService,
    IPersistedGrantService persistedGrantService,
    TimeProvider timeProvider
) : ICommandHandler<IntrospectTokenCommand>, ISupportMediatorPriority
{
    private IIssuedTokenValidator IssuedTokenValidator { get; } = issuedTokenValidator;
    private ISubjectIdentifierService SubjectIdentifierService { get; } = subjectIdentifierService;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        IntrospectTokenCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, resourceServer, request, result) = command;

        // A higher-priority handler already resolved the token.
        if (result.Active)
        {
            return;
        }

        var token = request.Token;
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        // Try the JWT access token first (the common introspection target); otherwise a refresh token grant.
        if (
            resourceServer is not null
            && await TryIntrospectAccessTokenAsync(
                openIdContext,
                resourceServer,
                token,
                result,
                cancellationToken
            )
        )
        {
            return;
        }

        await TryIntrospectRefreshTokenAsync(
            openIdContext,
            openIdClient,
            token,
            result,
            cancellationToken
        );
    }

    private async ValueTask<bool> TryIntrospectAccessTokenAsync(
        OpenIdContext openIdContext,
        ResolvedResourceServer resourceServer,
        string token,
        IntrospectionResult result,
        CancellationToken cancellationToken
    )
    {
        var options = new AccessTokenValidationOptions
        {
            RequiredAudiences = [resourceServer.Identifier],
            AdditionalDecryptionKeys = resourceServer.SecretsProvider.Collection,
        };

        var validateResult = await IssuedTokenValidator.ValidateAccessTokenAsync(
            openIdContext,
            token,
            options,
            cancellationToken
        );

        // A token that cannot be decoded as an access token for the caller's resource server is not one; fall through to
        // the refresh-token path.
        if (!validateResult.IsValid)
        {
            return false;
        }

        var payload = validateResult.DecodedJwt.Payload;

        result.Active = true;
        foreach (var claim in payload.EnumerateObject())
        {
            result.Claims[claim.Name] = claim.Value.Clone();
        }

        // The caller learns only its own audience and the scopes it owns, never the other resource servers the token
        // also targets.
        result.Claims[JoseClaimNames.Payload.Aud] = resourceServer.Identifier;
        NarrowScope(payload, resourceServer, result);

        return true;
    }

    private static void NarrowScope(
        JsonElement payload,
        ResolvedResourceServer resourceServer,
        IntrospectionResult result
    )
    {
        if (!payload.TryGetPropertyValue<string>(JoseClaimNames.Payload.Scope, out var scope))
        {
            return;
        }

        var ownedScopes = resourceServer.Scopes;
        var narrowed = scope
            .Split(OpenIdProtocol.ParameterSeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .Where(value => ownedScopes.Contains(value, StringComparer.Ordinal))
            .ToList();

        if (narrowed.Count == 0)
        {
            result.Claims.Remove(JoseClaimNames.Payload.Scope);
            return;
        }

        result.Claims[JoseClaimNames.Payload.Scope] = string.Join(
            OpenIdProtocol.ParameterSeparatorChar,
            narrowed
        );
    }

    private async ValueTask TryIntrospectRefreshTokenAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string token,
        IntrospectionResult result,
        CancellationToken cancellationToken
    )
    {
        var grantId = PersistedGrantService.CreateGrantId(
            openIdContext.Tenant.TenantId,
            OpenIdConstants.PersistedGrantTypes.RefreshToken,
            token
        );

        var grant = await PersistedGrantService.GetOrDefaultAsync<RefreshTokenGrant>(
            openIdContext,
            grantId,
            cancellationToken
        );

        if (grant is null || grant.Value.Status != PersistedGrantStatus.Active)
        {
            return;
        }

        // A refresh token is a credential of the client it was issued to, and only that client may learn about it.
        var payload = grant.Value.Payload;
        if (!string.Equals(payload.ClientId, openIdClient.ClientId, StringComparison.Ordinal))
        {
            return;
        }

        // A family past its horizon is refused by the refresh grant, so it is not active (ADR-0297).
        if (
            RefreshTokenExpiration.HasLineageExpired(
                openIdClient.Settings,
                payload.LineageCreatedWhen,
                TimeProvider.GetUtcNowWithPrecisionInSeconds()
            )
        )
        {
            return;
        }

        result.Active = true;

        result.Claims[JoseClaimNames.Payload.ClientId] = payload.ClientId;
        if (payload.EffectiveScopes.Count > 0)
        {
            result.Claims[JoseClaimNames.Payload.Scope] = string.Join(
                OpenIdProtocol.ParameterSeparatorChar,
                payload.EffectiveScopes
            );
        }

        if (grant.Value.SubjectId is { Length: > 0 } subjectId)
        {
            // Project to the client's per-sector pseudonym so a pairwise client never recovers the stable principal
            // identifier through introspection (OpenID Connect Core 1.0 §8). The access-token path needs no such
            // projection: the token already carries the projected `sub`.
            result.Claims[JoseClaimNames.Payload.Sub] = SubjectIdentifierService.ProjectSubjectId(
                openIdContext,
                openIdClient,
                subjectId
            );
        }
    }
}
