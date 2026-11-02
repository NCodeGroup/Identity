// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Authenticates the client to the provider's token and revocation endpoints with one method.
/// </summary>
/// <remarks>
/// Authenticators form an open strategy collection selected by <see cref="MethodName"/>: another package adds a method
/// by registering an implementation. The client does not check the method against
/// <c>token_endpoint_auth_methods_supported</c>, because the provider enforces the registered method.
/// </remarks>
[PublicAPI]
public interface IClientAuthenticator
{
    /// <summary>
    /// Gets the name of the authentication method, for example <see cref="OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic"/>.
    /// </summary>
    string MethodName { get; }

    /// <summary>
    /// Gets a value indicating whether the method authenticates the client with its TLS client certificate (RFC 8705 §2).
    /// When <see langword="true"/>, the request goes over a connection that presents the certificate from the client's
    /// key ring, to the provider's mTLS endpoint alias when it has one.
    /// </summary>
    bool UsesMutualTls { get; }

    /// <summary>
    /// Adds the client's credentials to the request headers or form parameters.
    /// </summary>
    /// <param name="context">The request to authenticate.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    ValueTask AuthenticateAsync(
        ClientAuthenticationContext context,
        CancellationToken cancellationToken
    );
}
