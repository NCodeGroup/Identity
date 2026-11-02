// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Client.Authentication;

/// <summary>
/// Provides an implementation of <see cref="IClientAuthenticator"/> for the <c>tls_client_auth</c> method (RFC 8705 §2.1),
/// where the provider validates the client certificate by PKI.
/// </summary>
internal class TlsClientAuthClientAuthenticator : MutualTlsClientAuthenticator
{
    /// <inheritdoc />
    public override string MethodName => OpenIdProtocol.ClientAuthenticationMethods.TlsClientAuth;
}
