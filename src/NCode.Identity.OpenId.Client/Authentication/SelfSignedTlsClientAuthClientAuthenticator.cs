// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Client.Authentication;

/// <summary>
/// Provides an implementation of <see cref="IClientAuthenticator"/> for the <c>self_signed_tls_client_auth</c> method
/// (RFC 8705 §2.2), where the provider matches a self-signed client certificate against the one registered for the client.
/// </summary>
internal class SelfSignedTlsClientAuthClientAuthenticator : MutualTlsClientAuthenticator
{
    /// <inheritdoc />
    public override string MethodName =>
        OpenIdProtocol.ClientAuthenticationMethods.SelfSignedTlsClientAuth;
}
