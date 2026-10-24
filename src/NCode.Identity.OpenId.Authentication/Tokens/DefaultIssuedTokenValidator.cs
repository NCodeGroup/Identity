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
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Authentication.Settings;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Keys;
using NCode.Json;

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Provides the default implementation of <see cref="IIssuedTokenValidator"/>.
/// </summary>
/// <remarks>
/// The algorithm lists are read from the tenant's effective settings. Every token the tenant issues is protected within
/// the lists of the client and resource servers it was issued for, which narrow the tenant's lists by intersection, so
/// the tenant's lists admit every token it issued and no algorithm it does not permit.
/// </remarks>
// See ADR-0167 (closing the follow-ups of ADR-0154 and ADR-0158, and the introspection clause of ADR-0112).
internal class DefaultIssuedTokenValidator(
    IJwtValidator jwtValidator,
    ISessionService sessionService
) : IIssuedTokenValidator
{
    private IJwtValidator JwtValidator { get; } = jwtValidator;
    private ISessionService SessionService { get; } = sessionService;

    /// <inheritdoc />
    public async ValueTask<JwtValidationResult> ValidateAccessTokenAsync(
        OpenIdContext openIdContext,
        string accessToken,
        AccessTokenValidationOptions options,
        CancellationToken cancellationToken
    )
    {
        var openIdTenant = openIdContext.Tenant;
        var settings = openIdTenant.SettingsProvider.Collection;
        var tenantKeys = openIdTenant.SecretsProvider.Collection;

        IReadOnlyCollection<SecretKey> decryptionKeys =
            options.AdditionalDecryptionKeys.Count == 0
                ? tenantKeys
                : [.. tenantKeys, .. options.AdditionalDecryptionKeys];

        // The realm and ownership handlers read roles with ClaimsPrincipal.IsInRole, which keys off the identity's
        // role-claim type; align it with the token's 'role' claim so a 'GlobalAdmin' role is recognized.
        var parameters = new JwtValidationParameters
        {
            RoleClaimType = JoseClaimNames.Payload.Role,
            ValidSignatureAlgorithms = settings.GetPermittedAlgorithms(
                OpenIdSettingKeys.AccessTokenSigningAlgValuesSupported
            ),
            ValidKeyManagementAlgorithms = settings.GetPermittedAlgorithms(
                OpenIdSettingKeys.AccessTokenEncryptionAlgValuesSupported
            ),
            ValidContentEncryptionAlgorithms = settings.GetPermittedAlgorithms(
                OpenIdSettingKeys.AccessTokenEncryptionEncValuesSupported
            ),
            ValidCompressionAlgorithms = [],
        }
            .UseValidationKeys(tenantKeys)
            .UseDecryptionKeys(decryptionKeys)
            .ValidateTokenType(JoseTokenTypes.AccessTokenJwt)
            .ValidateIssuer(openIdTenant.Issuer)
            .ValidateTokenLifeTime();

        if (options.RequiredAudiences.Count > 0)
        {
            parameters.ValidateAudience(options.RequiredAudiences);
        }

        var result = await ValidateAsync(accessToken, parameters, cancellationToken);
        if (!result.IsValid)
        {
            return result;
        }

        var isSessionRevoked = await IsSessionRevokedAsync(
            openIdContext,
            result.DecodedJwt.Payload,
            cancellationToken
        );

        return isSessionRevoked
            ? JwtValidationResult.Fail(
                parameters,
                result.PropertyBag,
                accessToken,
                new JwtValidationException(
                    "The end-user session the access token was issued under has been revoked."
                )
            )
            : result;
    }

    /// <inheritdoc />
    public async ValueTask<JwtValidationResult> ValidateIdTokenAsync(
        OpenIdContext openIdContext,
        string idToken,
        CancellationToken cancellationToken
    )
    {
        var parameters = CreateIdTokenParameters(openIdContext).ValidateTokenLifeTime();
        return await ValidateAsync(idToken, parameters, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<JwtValidationResult> ValidateIdTokenHintAsync(
        OpenIdContext openIdContext,
        string idTokenHint,
        CancellationToken cancellationToken
    )
    {
        var openIdTenant = openIdContext.Tenant;
        var settings = openIdTenant.SettingsProvider.Collection;

        // A relying party that received an encrypted ID token may re-encrypt it to the server (RP-Initiated Logout 1.0
        // §2), so a hint is decrypted with the tenant's keys under the tenant's advertised ID-token encryption lists.
        var parameters = CreateIdTokenParameters(openIdContext)
            .UseDecryptionKeys(openIdTenant.SecretsProvider.Collection);
        parameters.ValidKeyManagementAlgorithms = settings.GetPermittedAlgorithms(
            OpenIdSettingKeys.IdTokenEncryptionAlgValuesSupported
        );
        parameters.ValidContentEncryptionAlgorithms = settings.GetPermittedAlgorithms(
            OpenIdSettingKeys.IdTokenEncryptionEncValuesSupported
        );
        parameters.ValidCompressionAlgorithms = [];

        return await ValidateAsync(idTokenHint, parameters, cancellationToken);
    }

    // An ID token is encrypted only to the public key of its client, so the server names no decryption keys and an
    // encrypted ID token is rejected; only a hint, which its client may re-encrypt to the server, is decrypted.
    private static JwtValidationParameters CreateIdTokenParameters(OpenIdContext openIdContext)
    {
        var openIdTenant = openIdContext.Tenant;
        var settings = openIdTenant.SettingsProvider.Collection;

        return new JwtValidationParameters
        {
            RoleClaimType = JoseClaimNames.Payload.Role,
            ValidSignatureAlgorithms = settings.GetPermittedAlgorithms(
                OpenIdSettingKeys.IdTokenSigningAlgValuesSupported
            ),
        }
            .UseValidationKeys(openIdTenant.SecretsProvider.Collection)
            .ValidateTokenType(JoseTokenTypes.Jwt)
            .ValidateIssuer(openIdTenant.Issuer);
    }

    private async ValueTask<JwtValidationResult> ValidateAsync(
        string token,
        JwtValidationParameters parameters,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await JwtValidator.ValidateAsync(token, parameters, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return JwtValidationResult.Fail(parameters, parameters.PropertyBag, token, exception);
        }
    }

    // Only an explicitly revoked session invalidates the token: an absent (aged-out) session grant is not a revocation,
    // and the access token's own short lifetime bounds it.
    private async ValueTask<bool> IsSessionRevokedAsync(
        OpenIdContext openIdContext,
        JsonElement payload,
        CancellationToken cancellationToken
    )
    {
        if (
            !payload.TryGetPropertyValue<string>(JoseClaimNames.Payload.Sid, out var sessionId)
            || string.IsNullOrEmpty(sessionId)
        )
        {
            return false;
        }

        var session = await SessionService.GetOrDefaultAsync(
            openIdContext,
            sessionId,
            cancellationToken
        );

        return session is { Status: PersistedGrantStatus.Revoked };
    }
}
