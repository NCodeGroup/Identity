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

using NCode.Identity.Jose.Extensions;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Logic;
using NCode.Identity.OpenId.Logic.Authorization;
using NCode.Identity.OpenId.Models;
using NCode.Identity.OpenId.Subject;
using NCode.Identity.OpenId.Tokens.Commands;
using NCode.Identity.OpenId.Tokens.Models;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Logic;

/// <summary>
/// Provides a default implementation of the <see cref="IAuthorizationCodeService"/> abstraction.
/// </summary>
public class DefaultAuthorizationCodeService(
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    IPersistedGrantService persistedGrantService
) : IAuthorizationCodeService
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;

    /// <inheritdoc />
    public async ValueTask<SecurityToken> CreateAuthorizationCodeAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        SubjectAuthentication subjectAuthentication,
        CancellationToken cancellationToken)
    {
        var mediator = openIdContext.Mediator;

        var tenantId = openIdContext.Tenant.TenantId;
        var clientId = openIdClient.ClientId;
        var subjectId = subjectAuthentication.SubjectId;

        var authorizationCode = CryptoService.GenerateUrlSafeKey();

        var persistedGrantId = new PersistedGrantId
        {
            TenantId = tenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.AuthorizationCode,
            GrantKey = authorizationCode
        };

        var persistedGrant = new PersistedGrant<IAuthorizationRequest>
        {
            ClientId = clientId,
            SubjectId = subjectId,
            Payload = authorizationRequest
        };

        var createdWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        var lifetime = openIdClient.Settings.AuthorizationCodeLifetime;

        await PersistedGrantService.AddAsync(
            persistedGrantId,
            persistedGrant,
            createdWhen,
            lifetime,
            cancellationToken);

        var tokenPeriod = new TimePeriod(createdWhen, createdWhen + lifetime);
        var securityToken = new SecurityToken(
            OpenIdConstants.SecurityTokenTypes.AuthorizationCode,
            authorizationCode,
            tokenPeriod);

        await mediator.SendAsync(
            new SecurityTokenIssuedEvent(openIdContext, openIdClient, subjectId, securityToken),
            cancellationToken);

        return securityToken;
    }
}
