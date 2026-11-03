// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
    IClientAuthenticationAttemptLimiter attemptLimiter,
    ILogger<ClientSecretPostClientAuthenticationHandler> logger
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
    public override string AuthenticationMethod =>
        OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost;

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
            !form.TryGetValue(OpenIdProtocol.Parameters.ClientId, out var clientId)
            || !form.TryGetValue(OpenIdProtocol.Parameters.ClientSecret, out var clientSecret)
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
