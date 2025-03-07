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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.Logic;

namespace NCode.Identity.OpenId.Clients.Handlers;

/// <summary>
/// Provides an implementation of <see cref="IClientAuthenticationHandler"/> that uses <c>client_id</c> from the HTTP Request Query.
/// This handler also ensures that the <c>client_secret</c> is not passed in the query string.
/// </summary>
public class NoneClientAuthenticationHandler(
    IStoreManagerFactory storeManagerFactory,
    IOpenIdClientFactory clientFactory,
    ISettingSerializer settingSerializer,
    ISecretSerializer secretSerializer
) :
    CommonClientAuthenticationHandler(storeManagerFactory, clientFactory, settingSerializer, secretSerializer),
    IClientAuthenticationHandler
{
    /// <inheritdoc />
    public override string AuthenticationMethod => OpenIdConstants.ClientAuthenticationMethods.None;

    /// <inheritdoc />
    public override async ValueTask<ClientAuthenticationResult> AuthenticateClientAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken)
    {
        var httpRequest = openIdContext.Http.Request;

        if (!HttpMethods.IsGet(httpRequest.Method))
            return ClientAuthenticationResult.Undefined;

        var query = httpRequest.Query;

        if (!query.TryGetValue(OpenIdConstants.Parameters.ClientId, out var clientId))
            return ClientAuthenticationResult.Undefined;

        if (query.ContainsKey(OpenIdConstants.Parameters.ClientSecret))
            return new ClientAuthenticationResult(
                openIdContext.ErrorFactory
                    .InvalidRequest("The client secret must not be passed in the query string.")
                    .WithStatusCode(StatusCodes.Status400BadRequest)
            );

        return await AuthenticateClientAsync(
            openIdContext,
            clientId.ToString(),
            clientSecret: ReadOnlyMemory<char>.Empty,
            hasClientSecret: false,
            cancellationToken);
    }
}
