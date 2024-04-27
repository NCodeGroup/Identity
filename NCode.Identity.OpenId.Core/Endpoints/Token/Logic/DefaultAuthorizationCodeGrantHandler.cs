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
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Logic;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Endpoints.Token.Logic;

internal class DefaultAuthorizationCodeGrantHandler(
    IOpenIdErrorFactory errorFactory,
    IPersistedGrantService persistedGrantService
) : ITokenGrantHandler
{
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;

    /// <inheritdoc />
    public string GrantType => OpenIdConstants.GrantTypes.AuthorizationCode;

    /// <inheritdoc />
    public async ValueTask<IResult> HandleAsync(TokenRequestContext tokenRequestContext, CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, tokenRequest) = tokenRequestContext;
        var clientSettings = openIdClient.Settings;

        var clientId = openIdClient.ClientId;
        var authorizationCode = tokenRequest.Code;

        // TODO: verify error codes and descriptions

        if (string.IsNullOrEmpty(authorizationCode))
            return ErrorFactory.InvalidParameterValue("The authorization code is missing.").AsResult();

        var grantId = new PersistedGrantId
        {
            TenantId = openIdContext.Tenant.TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.AuthorizationCode,
            GrantKey = authorizationCode
        };

        var grantOrNull = await PersistedGrantService.TryGetAsync<IAuthorizationRequest>(
            grantId,
            singleUse: true,
            setConsumed: true,
            cancellationToken);

        if (!grantOrNull.HasValue)
            return ErrorFactory.InvalidRequest("TODO").AsResult();

        var grant = grantOrNull.Value;
        var authorizationRequest = grant.Payload;

        if (grant.ClientId != clientId)
            return ErrorFactory.InvalidRequest("TODO").AsResult();

        if (authorizationRequest.ClientId != clientId)
            return ErrorFactory.InvalidRequest("TODO").AsResult();

        // TODO: issue token(s)

        throw new NotImplementedException();
    }
}
