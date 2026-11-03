// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the <see cref="ClientAuthenticationMethodDescriptor"/> of each built-in client-authentication method, in
/// the order that the server advertises them in <c>token_endpoint_auth_methods_supported</c>. Each descriptor has the
/// name of the <see cref="IClientAuthenticationHandler"/> that authenticates the method.
/// </summary>
// See ADR-1207.
internal sealed class DefaultClientAuthenticationMethodDataSource(INullChangeToken nullChangeToken)
    : ICollectionDataSource<ClientAuthenticationMethodDescriptor>
{
    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<ClientAuthenticationMethodDescriptor> Collection
    {
        get
        {
            yield return new ClientAuthenticationMethodDescriptor
            {
                Name = OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
                RequiresClientSecret = true,
                UsesClientSecretAsKey = false,
            };

            yield return new ClientAuthenticationMethodDescriptor
            {
                Name = OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
                RequiresClientSecret = true,
                UsesClientSecretAsKey = false,
            };

            yield return new ClientAuthenticationMethodDescriptor
            {
                Name = OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt,
                RequiresClientSecret = false,
                UsesClientSecretAsKey = false,
            };

            yield return new ClientAuthenticationMethodDescriptor
            {
                Name = OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt,
                RequiresClientSecret = true,
                UsesClientSecretAsKey = true,
            };

            yield return new ClientAuthenticationMethodDescriptor
            {
                Name = OpenIdProtocol.ClientAuthenticationMethods.TlsClientAuth,
                RequiresClientSecret = false,
                UsesClientSecretAsKey = false,
            };

            yield return new ClientAuthenticationMethodDescriptor
            {
                Name = OpenIdProtocol.ClientAuthenticationMethods.SelfSignedTlsClientAuth,
                RequiresClientSecret = false,
                UsesClientSecretAsKey = false,
            };

            yield return new ClientAuthenticationMethodDescriptor
            {
                Name = OpenIdProtocol.ClientAuthenticationMethods.None,
                RequiresClientSecret = false,
                UsesClientSecretAsKey = false,
            };
        }
    }
}
