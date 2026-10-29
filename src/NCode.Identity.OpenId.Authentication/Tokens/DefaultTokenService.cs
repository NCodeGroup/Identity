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

using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Primitives;
using NCode.Identity.Cryptography;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.Models;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenService"/> abstraction.
/// </summary>
internal class DefaultTokenService(
    ICryptoService cryptoService,
    IClientJwtCredentialSelector clientJwtCredentialSelector,
    IAccessTokenPolicyService accessTokenPolicyService,
    IJwtEncoder jwtEncoder,
    IPersistedGrantService persistedGrantService,
    IResourceServerCredentialResolver resourceServerCredentialResolver,
    IAuditEventRecorder auditEventRecorder
) : ITokenService
{
    private ICryptoService CryptoService { get; } = cryptoService;
    private IClientJwtCredentialSelector ClientJwtCredentialSelector { get; } =
        clientJwtCredentialSelector;
    private IAccessTokenPolicyService AccessTokenPolicyService { get; } = accessTokenPolicyService;
    private IJwtEncoder JwtEncoder { get; } = jwtEncoder;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private IResourceServerCredentialResolver ResourceServerCredentialResolver { get; } =
        resourceServerCredentialResolver;
    private IAuditEventRecorder AuditEventRecorder { get; } = auditEventRecorder;

    private readonly record struct PreparedRefreshToken(
        string Value,
        PersistedGrantId GrantId,
        PersistedGrant<RefreshTokenGrant> Grant,
        DateTimeOffset CreatedWhen,
        TimeSpan Lifetime,
        DateTimeOffset LineageExpiresWhen,
        string? SubjectId
    );

    private static IEnumerable<Claim> FilterClaims(
        IReadOnlySettingCollection settings,
        IEnumerable<Claim> claims
    )
    {
        if (
            !settings.TryGetValue(
                OpenIdSettingKeys.ClaimsSupportedIsStrict,
                out var claimsSupportedIsStrict
            ) || !claimsSupportedIsStrict
        )
        {
            return claims;
        }

        if (
            !settings.TryGetValue(OpenIdSettingKeys.ClaimsSupported, out var claimsSupported)
            || claimsSupported.Count == 0
        )
        {
            return claims;
        }

        var supported =
            claimsSupported as IReadOnlySet<string>
            ?? new HashSet<string>(claimsSupported, StringComparer.Ordinal);
        var filtered = claims.Where(claim => supported.Contains(claim.Type));

        return filtered;
    }

    private static bool IsEssentialAuthTime(IRequestClaims? requestClaims) =>
        requestClaims?.IdToken is { } idTokenClaims
        && idTokenClaims.TryGetValue(JoseClaimNames.Payload.AuthTime, out var requestClaim)
        && requestClaim?.Essential == true;

    // Guarantees the 'auth_time' claim is present in the ID token for the cases where OpenID Connect makes it
    // mandatory (a max_age request or an essential auth_time claim request), synthesizing it from the authentication
    // properties when the subject principal did not already carry one.
    private static IEnumerable<Claim> EnsureAuthTime(
        OpenIdContext openIdContext,
        SubjectAuthentication ticket,
        CreateSecurityTokenRequest tokenRequest,
        IEnumerable<Claim> claims
    )
    {
        var hasAuthTime = false;

        foreach (var claim in claims)
        {
            hasAuthTime = hasAuthTime || claim.Type == JoseClaimNames.Payload.AuthTime;

            yield return claim;
        }

        // ReSharper disable once InvertIf
        if (!hasAuthTime)
        {
            var subject = ticket.Subject;
            var authenticationProperties = ticket.AuthenticationProperties;

            var issuer = openIdContext.Tenant.Issuer;
            var authTime = authenticationProperties.IssuedUtc ?? tokenRequest.CreatedWhen;

            var identity = openIdContext.Environment.GetSubjectIdentity(subject);

            var claim = new Claim(
                JoseClaimNames.Payload.AuthTime,
                authTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64,
                issuer,
                issuer,
                identity
            );

            yield return claim;
        }
    }

    /// <inheritdoc />
    public async ValueTask<SecurityToken> CreateAccessTokenAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CreateSecurityTokenRequest tokenRequest,
        CancellationToken cancellationToken
    )
    {
        var mediator = openIdContext.Mediator;
        var openIdTenant = openIdContext.Tenant;

        // Resolve the resource servers this token targets: they bind its audience (ADR-0027), and their settings bound
        // the token together with the client's (ADR-0158). Disposed after issuance so their transient secret-key
        // providers are released.
        await using var resolution = await ResourceServerCredentialResolver.ResolveAsync(
            openIdContext,
            tokenRequest.EffectiveScopes,
            tokenRequest.Resources ?? [],
            cancellationToken
        );

        // Every access-token setting is read from the combined settings, and the token's credentials are chosen within
        // every bound — or the request is refused (ADR-0158).
        var policy = AccessTokenPolicyService.Evaluate(
            openIdContext,
            openIdClient,
            tokenRequest.Confirmation,
            resolution
        );
        var settings = policy.Settings;
        var signingCredentials = policy.SigningCredentials;
        var encryptionCredentials = policy.EncryptionCredentials;

        var ticket = tokenRequest.SubjectAuthentication;
        var tokenContext = new SecurityTokenContext(
            tokenRequest,
            signingCredentials,
            encryptionCredentials,
            OpenIdConstants.SecurityTokenTypes.AccessToken
        );

        var subjectClaims = new List<Claim>();
        var payloadClaims = CreatePayloadClaims(tokenRequest.ExtraPayloadClaims);

        await mediator.SendAsync(
            new GetAccessTokenSubjectClaimsCommand(
                openIdContext,
                openIdClient,
                tokenContext,
                subjectClaims
            ),
            cancellationToken
        );

        await mediator.SendAsync(
            new GetAccessTokenPayloadClaimsCommand(
                openIdContext,
                openIdClient,
                tokenContext,
                payloadClaims
            ),
            cancellationToken
        );

        if (
            settings.TryGetValue(
                OpenIdSettingKeys.SendIdClaimsInAccessToken,
                out var sendIdClaimsInAccessToken
            ) && sendIdClaimsInAccessToken
        )
        {
            await mediator.SendAsync(
                new GetIdTokenSubjectClaimsCommand(
                    openIdContext,
                    openIdClient,
                    tokenContext,
                    subjectClaims
                ),
                cancellationToken
            );
        }

        var lifetime = settings.GetValue(OpenIdSettingKeys.AccessTokenLifetime);
        var createdWhen = tokenRequest.CreatedWhen;
        var expiresWhen = createdWhen + lifetime;
        var tokenLifetime = new TimePeriod { StartTime = createdWhen, EndTime = expiresWhen };

        await PersistUserInfoClaimsGrantAsync(
            openIdContext,
            openIdClient,
            tokenRequest,
            payloadClaims,
            createdWhen,
            lifetime,
            cancellationToken
        );

        // auth_time, when known, flows into the access token via the subject-claims handler (RFC 9068 includes it
        // when available); it is not synthesized here.
        var effectiveClaims = FilterClaims(settings, subjectClaims);

        // Bind the access token audience to the resource servers resolved above (ADR-0027); fall back to the client
        // when no resource-server audience applies (for example, a scopeless token).
        StringValues audience =
            resolution.Audiences.Count > 0
                ? new StringValues(resolution.Audiences.ToArray())
                : new StringValues(openIdClient.ClientId);

        var parameters = new JwtEncodingParameters
        {
            TokenType = settings.GetValue(OpenIdSettingKeys.AccessTokenType),

            SigningCredentials = signingCredentials,
            EncryptionCredentials = encryptionCredentials,

            Issuer = openIdTenant.Issuer,
            Audience = audience,

            IssuedAt = createdWhen,
            NotBefore = createdWhen,
            Expires = expiresWhen,

            SubjectClaims = effectiveClaims,
            ExtraPayloadClaims = payloadClaims,

            ExtraSignatureHeaderClaims = tokenRequest.ExtraSignatureHeaderClaims,
            ExtraEncryptionHeaderClaims = tokenRequest.ExtraEncryptionHeaderClaims,
        };

        var jwt = JwtEncoder.Encode(parameters);
        var securityToken = new SecurityToken
        {
            TokenType = tokenContext.TokenType,
            TokenValue = jwt,
            TokenLifetime = tokenLifetime,
        };
        var subjectId = tokenRequest.SubjectAuthentication?.PrincipalId;

        await AuditEventRecorder.RecordTokenIssuedAsync(
            openIdContext,
            openIdClient,
            subjectId,
            securityToken,
            cancellationToken
        );

        return securityToken;
    }

    private async ValueTask PersistUserInfoClaimsGrantAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CreateSecurityTokenRequest tokenRequest,
        Dictionary<string, object> payloadClaims,
        DateTimeOffset createdWhen,
        TimeSpan lifetime,
        CancellationToken cancellationToken
    )
    {
        // Persist the UserInfo claims requested via the 'claims' parameter so the UserInfo endpoint can honor them,
        // keyed by the access token's jti and bounded by its lifetime (ADR-0039). Nothing is stored when none were
        // requested, so the common case adds no write.
        var requestClaims = tokenRequest.RequestClaims;
        var userInfoClaims = requestClaims?.UserInfo;
        if (requestClaims is null || userInfoClaims is null || userInfoClaims.Count == 0)
        {
            return;
        }

        if (
            !payloadClaims.TryGetValue(JoseClaimNames.Payload.Jti, out var jtiValue)
            || jtiValue is not string jti
            || string.IsNullOrEmpty(jti)
        )
        {
            return;
        }

        var tenantId = openIdContext.Tenant.TenantId;
        var grantId = PersistedGrantService.CreateGrantId(
            tenantId,
            OpenIdConstants.PersistedGrantTypes.UserInfoClaims,
            jti
        );

        var grant = new PersistedGrant<IRequestClaims>
        {
            TenantId = tenantId,
            ClientId = openIdClient.ClientId,
            SubjectId = tokenRequest.SubjectAuthentication?.PrincipalId,
            SessionId = tokenRequest.SubjectAuthentication?.SessionId,
            Payload = requestClaims,
        };

        await PersistedGrantService.AddAsync(
            openIdContext,
            grantId,
            grant,
            createdWhen,
            lifetime,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<SecurityToken> CreateIdTokenAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CreateSecurityTokenRequest tokenRequest,
        CancellationToken cancellationToken
    )
    {
        var settings = openIdClient.Settings;
        var mediator = openIdContext.Mediator;
        var openIdTenant = openIdContext.Tenant;

        // An ID token is addressed to the client: signed with the tenant's key using the client's registered
        // id_token_signed_response_alg and, when the client registers an encryption algorithm, encrypted to the client's
        // public key (ADR-0155).
        var credentials = await ClientJwtCredentialSelector.SelectCredentialsAsync(
            openIdContext,
            openIdClient,
            ClientJwtProtectionSettings.IdToken,
            cancellationToken
        );

        var signingCredentials =
            credentials.SigningCredentials
            ?? throw new JoseCredentialsNotFoundException(
                "An ID token is always signed, but the client's 'id_token_signed_response_alg' is unset."
            );
        var encryptionCredentials = credentials.EncryptionCredentials;

        var ticket = tokenRequest.SubjectAuthentication;
        var tokenContext = new SecurityTokenContext(
            tokenRequest,
            signingCredentials,
            encryptionCredentials,
            OpenIdConstants.SecurityTokenTypes.IdToken
        );

        var subjectClaims = new List<Claim>();
        var payloadClaims = CreatePayloadClaims(tokenRequest.ExtraPayloadClaims);

        await mediator.SendAsync(
            new GetIdTokenSubjectClaimsCommand(
                openIdContext,
                openIdClient,
                tokenContext,
                subjectClaims
            ),
            cancellationToken
        );

        await mediator.SendAsync(
            new GetIdTokenPayloadClaimsCommand(
                openIdContext,
                openIdClient,
                tokenContext,
                payloadClaims
            ),
            cancellationToken
        );

        var lifetime = settings.GetValue(OpenIdSettingKeys.IdTokenLifetime);
        var createdWhen = tokenRequest.CreatedWhen;
        var expiresWhen = createdWhen + lifetime;
        var tokenLifetime = new TimePeriod { StartTime = createdWhen, EndTime = expiresWhen };

        var filteredClaims = FilterClaims(settings, subjectClaims);

        // When auth_time is required (a max_age request or an essential auth_time claim request) guarantee it even if
        // the subject principal did not carry one; otherwise it flows (or not) via the subject-claims handler per the
        // always_include_auth_time setting.
        var effectiveClaims =
            ticket.HasValue
            && (tokenRequest.AuthTimeRequired || IsEssentialAuthTime(tokenRequest.RequestClaims))
                ? EnsureAuthTime(openIdContext, ticket.Value, tokenRequest, filteredClaims)
                : filteredClaims;

        var parameters = new JwtEncodingParameters
        {
            SigningCredentials = signingCredentials,
            EncryptionCredentials = encryptionCredentials,

            Issuer = openIdTenant.Issuer,
            Audience = openIdClient.ClientId,

            IssuedAt = createdWhen,
            NotBefore = createdWhen,
            Expires = expiresWhen,

            SubjectClaims = effectiveClaims,
            ExtraPayloadClaims = payloadClaims,

            ExtraSignatureHeaderClaims = tokenRequest.ExtraSignatureHeaderClaims,
            ExtraEncryptionHeaderClaims = tokenRequest.ExtraEncryptionHeaderClaims,
        };

        var jwt = JwtEncoder.Encode(parameters);
        var securityToken = new SecurityToken
        {
            TokenType = tokenContext.TokenType,
            TokenValue = jwt,
            TokenLifetime = tokenLifetime,
        };

        var subjectId = tokenRequest.SubjectAuthentication?.PrincipalId;

        await AuditEventRecorder.RecordTokenIssuedAsync(
            openIdContext,
            openIdClient,
            subjectId,
            securityToken,
            cancellationToken
        );

        return securityToken;
    }

    /// <inheritdoc />
    public async ValueTask<IssuedRefreshToken> CreateRefreshTokenAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CreateSecurityTokenRequest tokenRequest,
        CancellationToken cancellationToken
    )
    {
        var prepared = PrepareRefreshToken(openIdContext, openIdClient, tokenRequest);

        await PersistedGrantService.AddAsync(
            openIdContext,
            prepared.GrantId,
            prepared.Grant,
            prepared.CreatedWhen,
            prepared.Lifetime,
            cancellationToken
        );

        return await AuditIssuedRefreshTokenAsync(
            openIdContext,
            openIdClient,
            prepared,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<IssuedRefreshToken?> RotateRefreshTokenAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        PersistedGrantId presentedGrantId,
        CreateSecurityTokenRequest tokenRequest,
        CancellationToken cancellationToken
    )
    {
        var prepared = PrepareRefreshToken(openIdContext, openIdClient, tokenRequest);

        var replacementGrantKey = await PersistedGrantService.ReplaceOnceOrDefault(
            openIdContext,
            presentedGrantId,
            prepared.GrantId,
            prepared.Grant,
            prepared.CreatedWhen,
            prepared.Lifetime,
            cancellationToken
        );

        if (replacementGrantKey is null)
            return null;

        return await AuditIssuedRefreshTokenAsync(
            openIdContext,
            openIdClient,
            prepared,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<IssuedRefreshToken?> ReissueRefreshTokenAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CreateSecurityTokenRequest tokenRequest,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(tokenRequest.RefreshTokenLineageId))
            throw new ArgumentException(
                "The request must name the refresh-token rotation family to join.",
                nameof(tokenRequest)
            );

        var prepared = PrepareRefreshToken(openIdContext, openIdClient, tokenRequest);

        var grantKey = await PersistedGrantService.AddToRefreshTokenLineageOrDefault(
            openIdContext,
            prepared.GrantId,
            prepared.Grant,
            prepared.CreatedWhen,
            prepared.Lifetime,
            cancellationToken
        );

        if (grantKey is null)
            return null;

        return await AuditIssuedRefreshTokenAsync(
            openIdContext,
            openIdClient,
            prepared,
            cancellationToken
        );
    }

    private PreparedRefreshToken PrepareRefreshToken(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CreateSecurityTokenRequest tokenRequest
    )
    {
        var settings = openIdClient.Settings;

        var subjectAuthentication = tokenRequest.SubjectAuthentication;

        var tenantId = openIdContext.Tenant.TenantId;
        var clientId = openIdClient.ClientId;
        var subjectId = subjectAuthentication?.PrincipalId;

        var refreshToken = CryptoService.GenerateUrlSafeKey(); // 256 bits of entropy

        // A rotation inherits the prior token's lineage; a fresh issuance starts a new family.
        var refreshTokenLineageId =
            tokenRequest.RefreshTokenLineageId ?? CryptoService.GenerateResourceId();

        var persistedGrantId = PersistedGrantService.CreateGrantId(
            tenantId,
            OpenIdConstants.PersistedGrantTypes.RefreshToken,
            refreshToken
        );

        // A rotation or a retry joins the family it inherits, which expires when it always would; a fresh issuance
        // begins a family now. See ADR-0297.
        var createdWhen = tokenRequest.CreatedWhen;
        var lineageCreatedWhen = tokenRequest.RefreshTokenLineageCreatedWhen ?? createdWhen;
        var lineageExpiresWhen = RefreshTokenExpiration.GetLineageExpiresWhen(
            settings,
            lineageCreatedWhen
        );
        var expiresWhen = RefreshTokenExpiration.GetTokenExpiresWhen(
            settings,
            lineageExpiresWhen,
            createdWhen
        );
        if (expiresWhen <= createdWhen)
            throw new ArgumentException(
                "The refresh-token rotation family has expired.",
                nameof(tokenRequest)
            );

        var refreshTokenGrant = new RefreshTokenGrant(
            clientId,
            tokenRequest.OriginalScopes,
            tokenRequest.EffectiveScopes,
            subjectAuthentication,
            tokenRequest.Confirmation,
            lineageCreatedWhen
        );

        var persistedGrant = new PersistedGrant<RefreshTokenGrant>
        {
            TenantId = tenantId,
            ClientId = clientId,
            SubjectId = subjectId,
            SessionId = subjectAuthentication?.SessionId,
            RefreshTokenLineageId = refreshTokenLineageId,
            Payload = refreshTokenGrant,
        };

        return new PreparedRefreshToken(
            refreshToken,
            persistedGrantId,
            persistedGrant,
            createdWhen,
            expiresWhen - createdWhen,
            lineageExpiresWhen,
            subjectId
        );
    }

    private async ValueTask<IssuedRefreshToken> AuditIssuedRefreshTokenAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        PreparedRefreshToken prepared,
        CancellationToken cancellationToken
    )
    {
        var tokenLifetime = new TimePeriod
        {
            StartTime = prepared.CreatedWhen,
            EndTime = prepared.CreatedWhen + prepared.Lifetime,
        };

        var securityToken = new SecurityToken
        {
            TokenType = OpenIdConstants.SecurityTokenTypes.RefreshToken,
            TokenValue = prepared.Value,
            TokenLifetime = tokenLifetime,
        };

        await AuditEventRecorder.RecordTokenIssuedAsync(
            openIdContext,
            openIdClient,
            prepared.SubjectId,
            securityToken,
            cancellationToken
        );

        return new IssuedRefreshToken
        {
            Token = securityToken,
            LineageExpiresWhen = prepared.LineageExpiresWhen,
        };
    }

    private static Dictionary<string, object> CreatePayloadClaims(
        IReadOnlyDictionary<string, object>? initial
    )
    {
        return initial == null
            ? new Dictionary<string, object>(StringComparer.Ordinal)
            : new Dictionary<string, object>(initial, StringComparer.Ordinal);
    }
}
