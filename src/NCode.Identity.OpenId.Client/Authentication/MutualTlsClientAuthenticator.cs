// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Client.Authentication;

/// <summary>
/// Provides a base class for the RFC 8705 mutual-TLS <see cref="IClientAuthenticator"/> methods, which authenticate the
/// client with its TLS client certificate and send only the <c>client_id</c> form parameter.
/// </summary>
internal abstract class MutualTlsClientAuthenticator : IClientAuthenticator
{
    /// <inheritdoc />
    public abstract string MethodName { get; }

    /// <inheritdoc />
    public bool UsesMutualTls => true;

    /// <inheritdoc />
    public ValueTask AuthenticateAsync(
        ClientAuthenticationContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Parameters.Add(
            new KeyValuePair<string, string>(
                OpenIdProtocol.Parameters.ClientId,
                context.Registration.ClientId
            )
        );
        return ValueTask.CompletedTask;
    }
}
