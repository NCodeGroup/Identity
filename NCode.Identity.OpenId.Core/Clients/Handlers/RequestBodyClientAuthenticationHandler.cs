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
/// Provides an implementation of <see cref="IClientAuthenticationHandler"/> that uses <c>client_id</c> and <c>client_secret</c> from the HTTP Request Body.
/// </summary>
public class RequestBodyClientAuthenticationHandler(
    IOpenIdErrorFactory errorFactory,
    IStoreManagerFactory storeManagerFactory,
    IOpenIdClientFactory clientFactory,
    ISettingSerializer settingSerializer,
    ISecretSerializer secretSerializer
) : CommonClientAuthenticationHandler(errorFactory, storeManagerFactory, clientFactory, settingSerializer, secretSerializer),
    IClientAuthenticationHandler
{
    /// <inheritdoc />
    public override string AuthenticationMethod => OpenIdConstants.ClientAuthenticationMethods.RequestBody;

    /// <inheritdoc />
    public override async ValueTask<ClientAuthenticationResult> AuthenticateClientAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken)
    {
        var httpRequest = openIdContext.Http.Request;

        if (!HttpMethods.IsPost(httpRequest.Method))
            return ClientAuthenticationResult.Undefined;

        var form = await httpRequest.ReadFormAsync(cancellationToken);

        if (!form.TryGetValue(OpenIdConstants.Parameters.ClientId, out var clientId))
            return ClientAuthenticationResult.Undefined;

        var hasClientSecret = form.TryGetValue(OpenIdConstants.Parameters.ClientSecret, out var clientSecret);

        return await AuthenticateClientAsync(
            openIdContext,
            clientId.ToString(),
            clientSecret.ToString().AsMemory(),
            hasClientSecret,
            cancellationToken);
    }
}
