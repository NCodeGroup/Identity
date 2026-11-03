// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
    IClientAuthenticationAttemptLimiter attemptLimiter,
    ILogger<NoneClientAuthenticationHandler> logger
)
    : CommonClientAuthenticationHandler(
        clientResolver,
        clientFactory,
        auditEventRecorder,
        attemptLimiter,
        logger
    ),
        IClientAuthenticationHandler
{
    /// <inheritdoc />
    public override string AuthenticationMethod => OpenIdProtocol.ClientAuthenticationMethods.None;

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
            !form.TryGetValue(OpenIdProtocol.Parameters.ClientId, out var clientIdValues)
            || string.IsNullOrEmpty(clientIdValues.ToString())
            || form.ContainsKey(OpenIdProtocol.Parameters.ClientSecret)
            || form.ContainsKey(OpenIdProtocol.Parameters.ClientAssertion)
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
