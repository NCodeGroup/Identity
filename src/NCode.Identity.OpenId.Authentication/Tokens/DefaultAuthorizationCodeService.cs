// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cryptography;
using NCode.Identity.Models;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Provides a default implementation of the <see cref="IAuthorizationCodeService"/> abstraction.
/// </summary>
internal class DefaultAuthorizationCodeService(
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    IPersistedGrantService persistedGrantService,
    IAuditEventRecorder auditEventRecorder
) : IAuthorizationCodeService
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private IAuditEventRecorder AuditEventRecorder { get; } = auditEventRecorder;

    /// <inheritdoc />
    public async ValueTask<SecurityToken> CreateAuthorizationCodeAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        SubjectAuthentication subjectAuthentication,
        CancellationToken cancellationToken
    )
    {
        var tenantId = openIdContext.Tenant.TenantId;
        var clientId = openIdClient.ClientId;
        var subjectId = subjectAuthentication.PrincipalId;

        var authorizationCode = CryptoService.GenerateUrlSafeKey();

        var persistedGrantId = PersistedGrantService.CreateGrantId(
            tenantId,
            OpenIdConstants.PersistedGrantTypes.AuthorizationCode,
            authorizationCode
        );

        // The token endpoint resumes this grant as an AuthorizationGrant, so persist both the request and the
        // authenticated subject (not the request alone) to match that shape and carry the subject forward.
        var authorizationGrant = new AuthorizationGrant(
            authorizationRequest,
            subjectAuthentication
        );
        var persistedGrant = new PersistedGrant<AuthorizationGrant>
        {
            TenantId = tenantId,
            ClientId = clientId,
            SubjectId = subjectId,
            SessionId = subjectAuthentication.SessionId,
            Payload = authorizationGrant,
        };

        var createdWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        var lifetime = openIdClient.Settings.GetValue(OpenIdSettingKeys.AuthorizationCodeLifetime);

        await PersistedGrantService.AddAsync(
            openIdContext,
            persistedGrantId,
            persistedGrant,
            createdWhen,
            lifetime,
            cancellationToken
        );

        var tokenLifetime = new TimePeriod
        {
            StartTime = createdWhen,
            EndTime = createdWhen + lifetime,
        };

        var securityToken = new SecurityToken
        {
            TokenType = OpenIdConstants.SecurityTokenTypes.AuthorizationCode,
            TokenValue = authorizationCode,
            TokenLifetime = tokenLifetime,
        };

        await AuditEventRecorder.RecordTokenIssuedAsync(
            openIdContext,
            openIdClient,
            subjectId,
            securityToken,
            cancellationToken
        );

        return securityToken;
    }
}
