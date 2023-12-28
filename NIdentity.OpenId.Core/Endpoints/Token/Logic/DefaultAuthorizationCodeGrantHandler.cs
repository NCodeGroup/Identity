#region Copyright Preamble

// Copyright @ 2023 NCode Group
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

using Microsoft.AspNetCore.Http;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Endpoints.Token.Messages;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Endpoints.Token.Logic;

internal class DefaultAuthorizationCodeGrantHandler(
    ICryptoService cryptoService,
    IOpenIdErrorFactory errorFactory,
    IPersistedGrantService persistedGrantService
) : ITokenGrantHandler
{
    private ICryptoService CryptoService { get; } = cryptoService;
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;

    /// <inheritdoc />
    public string GrantType => OpenIdConstants.GrantTypes.AuthorizationCode;

    /// <inheritdoc />
    public async ValueTask<IResult> HandleAsync(TokenRequestContext tokenRequestContext, CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, tokenRequest) = tokenRequestContext;
        var clientSettings = openIdClient.Settings;

        var tenantId = openIdContext.Tenant.TenantId;
        var clientId = openIdClient.ClientId;

        const string grantType = OpenIdConstants.PersistedGrantTypes.AuthorizationCode;
        var grantKey = tokenRequest.Code;

        // TODO: verify error codes and descriptions

        if (string.IsNullOrEmpty(grantKey))
            return ErrorFactory.InvalidParameterValue("The authorization code is missing.").AsResult();

        var authorizationRequest = await PersistedGrantService.TryGetAsync<IAuthorizationRequest>(
            tenantId,
            grantType,
            grantKey,
            singleUse: true,
            setConsumed: true,
            cancellationToken);

        if (authorizationRequest is null)
            return ErrorFactory.InvalidRequest("TODO").AsResult();

        if (authorizationRequest.ClientId != clientId)
            return ErrorFactory.InvalidRequest("TODO").AsResult();

        throw new NotImplementedException();
    }
}
