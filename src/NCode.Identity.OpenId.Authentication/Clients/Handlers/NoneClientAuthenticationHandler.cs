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
/// Provides an implementation of <see cref="IClientAuthenticationHandler"/> for <c>none</c>: a public client (RFC 6749
/// §2.1) identifies itself with the <c>client_id</c> in the HTTP request body and presents no credential. The handler
/// owns a request only when it carries no <c>client_secret</c> or <c>client_assertion</c> and the client is registered
/// as public; the same request from a client registered for another method — a mutual-TLS client, or a confidential
/// client that omitted its credentials — is left to the other handlers, so it never authenticates as public.
/// </summary>
internal class NoneClientAuthenticationHandler(
    IOpenIdClientResolver clientResolver,
    IOpenIdClientFactory clientFactory,
    IAuditEventRecorder auditEventRecorder,
    ILogger<NoneClientAuthenticationHandler> logger
)
    : CommonClientAuthenticationHandler(clientResolver, clientFactory, auditEventRecorder, logger),
        IClientAuthenticationHandler
{
    /// <inheritdoc />
    public override string AuthenticationMethod => OpenIdConstants.ClientAuthenticationMethods.None;

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
            !form.TryGetValue(OpenIdConstants.Parameters.ClientId, out var clientIdValues)
            || string.IsNullOrEmpty(clientIdValues.ToString())
            || form.ContainsKey(OpenIdConstants.Parameters.ClientSecret)
            || form.ContainsKey(OpenIdConstants.Parameters.ClientAssertion)
        )
        {
            return ClientAuthenticationResult.Undefined;
        }

        var resolution = await ResolveClientAsync(
            openIdContext,
            clientIdValues.ToString(),
            cancellationToken
        );
        if (!resolution.HasClient)
            return resolution;

        var client = resolution.Client;

        if (
            !string.Equals(
                client.RegisteredAuthenticationMethod,
                AuthenticationMethod,
                StringComparison.Ordinal
            )
        )
        {
            return ClientAuthenticationResult.Undefined;
        }

        var rejection = await RejectUnlessPermittedAsync(openIdContext, client, cancellationToken);
        return rejection ?? resolution;
    }
}
