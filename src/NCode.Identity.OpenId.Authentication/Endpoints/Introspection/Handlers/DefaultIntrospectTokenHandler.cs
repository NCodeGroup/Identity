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

using NCode.Identity.Jose;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Results;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="IntrospectTokenCommand"/> message. It resolves
/// whether the presented token is active — a JWT access token (validated against the tenant keys) or a refresh token
/// (a persisted grant) — and contributes the token's claims. Additional handlers may enrich the response.
/// </summary>
internal class DefaultIntrospectTokenHandler(
    IJwtValidator jwtValidator,
    IOpenIdClientResolver openIdClientResolver,
    ISubjectIdentifierService subjectIdentifierService,
    IPersistedGrantService persistedGrantService
) : ICommandHandler<IntrospectTokenCommand>, ISupportMediatorPriority
{
    private IJwtValidator JwtValidator { get; } = jwtValidator;
    private IOpenIdClientResolver OpenIdClientResolver { get; } = openIdClientResolver;
    private ISubjectIdentifierService SubjectIdentifierService { get; } = subjectIdentifierService;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        IntrospectTokenCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, _, request, result) = command;

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
        if (await TryIntrospectAccessTokenAsync(openIdContext, token, result, cancellationToken))
        {
            return;
        }

        await TryIntrospectRefreshTokenAsync(openIdContext, token, result, cancellationToken);
    }

    private async ValueTask<bool> TryIntrospectAccessTokenAsync(
        OpenIdContext openIdContext,
        string token,
        IntrospectionResult result,
        CancellationToken cancellationToken
    )
    {
        var openIdTenant = openIdContext.Tenant;

        JwtValidationResult validateResult;
        try
        {
            var parameters = new JwtValidationParameters()
                .UseValidationKeys(openIdTenant.SecretsProvider.Collection)
                .ValidateIssuer(openIdTenant.Issuer)
                .ValidateTokenLifeTime();

            validateResult = await JwtValidator.ValidateAsync(token, parameters, cancellationToken);
        }
        catch
        {
            // A token that cannot be parsed as a JWT is not an access token; fall through to the refresh-token path.
            return false;
        }

        if (!validateResult.IsValid)
        {
            return false;
        }

        result.Active = true;
        foreach (var claim in validateResult.DecodedJwt.Payload.EnumerateObject())
        {
            result.Claims[claim.Name] = claim.Value.Clone();
        }

        return true;
    }

    private async ValueTask TryIntrospectRefreshTokenAsync(
        OpenIdContext openIdContext,
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

        result.Active = true;

        var payload = grant.Value.Payload;
        result.Claims[JoseClaimNames.Payload.ClientId] = payload.ClientId;
        if (payload.EffectiveScopes.Count > 0)
        {
            result.Claims[JoseClaimNames.Payload.Scope] = string.Join(
                OpenIdConstants.ParameterSeparatorChar,
                payload.EffectiveScopes
            );
        }

        if (grant.Value.SubjectId is { Length: > 0 } subjectId)
        {
            // Project to the owning client's per-sector pseudonym so a pairwise client never recovers the stable
            // principal identifier through introspection (OpenID Connect Core 1.0 §8). The access-token path needs no
            // such projection: the token already carries the projected `sub`.
            var openIdClient = await OpenIdClientResolver.ResolveClientOrDefaultAsync(
                openIdContext,
                payload.ClientId,
                cancellationToken
            );

            result.Claims[JoseClaimNames.Payload.Sub] = openIdClient is null
                ? subjectId
                : SubjectIdentifierService.ProjectSubjectId(openIdContext, openIdClient, subjectId);
        }
    }
}
