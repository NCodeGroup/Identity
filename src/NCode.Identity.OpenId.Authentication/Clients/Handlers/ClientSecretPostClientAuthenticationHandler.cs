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
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Clients.Handlers;

/// <summary>
/// Provides an implementation of <see cref="IClientAuthenticationHandler"/> for <c>client_secret_post</c>: the client
/// presents its <c>client_id</c> and <c>client_secret</c> in the HTTP request body. A request without a
/// <c>client_secret</c> carries no credential of this method, so the handler leaves it to the others.
/// </summary>
internal class ClientSecretPostClientAuthenticationHandler(
    IOpenIdClientResolver clientResolver,
    IOpenIdClientFactory clientFactory,
    IAuditEventRecorder auditEventRecorder,
    ILogger<ClientSecretPostClientAuthenticationHandler> logger
)
    : CommonClientAuthenticationHandler(clientResolver, clientFactory, auditEventRecorder, logger),
        IClientAuthenticationHandler
{
    /// <inheritdoc />
    public override string AuthenticationMethod =>
        OpenIdConstants.ClientAuthenticationMethods.ClientSecretPost;

    /// <inheritdoc />
    public override async ValueTask<ClientAuthenticationResult> AuthenticateClientAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken
    )
    {
        var httpRequest = openIdContext.Http.Request;

        if (!HttpMethods.IsPost(httpRequest.Method) || !httpRequest.HasFormContentType)
            return ClientAuthenticationResult.Undefined;

        var form = await httpRequest.ReadFormAsync(cancellationToken);

        if (
            !form.TryGetValue(OpenIdConstants.Parameters.ClientId, out var clientId)
            || !form.TryGetValue(OpenIdConstants.Parameters.ClientSecret, out var clientSecret)
        )
        {
            return ClientAuthenticationResult.Undefined;
        }

        return await AuthenticateClientSecretAsync(
            openIdContext,
            clientId.ToString(),
            clientSecret.ToString().AsMemory(),
            cancellationToken
        );
    }
}
