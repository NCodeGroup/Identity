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
using NCode.Identity.JsonWebTokens;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Results;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="IntrospectTokenCommand"/> message. It resolves
/// whether the presented token is active — a JWT access token (validated against the tenant keys) or a refresh token
/// (a persisted grant) — and contributes the token's claims. Additional handlers may enrich the response.
/// </summary>
internal class DefaultIntrospectTokenHandler(
    IJsonWebTokenService jsonWebTokenService,
    IPersistedGrantService persistedGrantService
) : ICommandHandler<IntrospectTokenCommand>, ISupportMediatorPriority
{
    private IJsonWebTokenService JsonWebTokenService { get; } = jsonWebTokenService;
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

        ValidateJwtResult validateResult;
        try
        {
            var parameters = new ValidateJwtParameters()
                .UseValidationKeys(openIdTenant.SecretsProvider.Collection)
                .ValidateIssuer(openIdTenant.Issuer)
                .ValidateTokenLifeTime();

            validateResult = await JsonWebTokenService.ValidateJwtAsync(
                token,
                parameters,
                cancellationToken
            );
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
            result.Claims[JoseClaimNames.Payload.Sub] = subjectId;
        }
    }
}
