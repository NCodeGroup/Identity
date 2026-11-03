// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Describes a client-authentication method: the name a client registers in its <c>token_endpoint_auth_method</c> and
/// the traits of the method that other components need without constructing its handler. A package contributes a
/// descriptor through <c>ICollectionDataSource&lt;ClientAuthenticationMethodDescriptor&gt;</c> beside each
/// <see cref="IClientAuthenticationHandler"/> it registers.
/// </summary>
// See ADR-1207.
[PublicAPI]
public sealed class ClientAuthenticationMethodDescriptor
{
    /// <summary>
    /// Gets the name of the method, which is the <c>token_endpoint_auth_method</c> value a client registers and the
    /// <see cref="IClientAuthenticationHandler.AuthenticationMethod"/> of the handler that authenticates it.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether the method verifies a shared secret, so that a dynamically registered client of
    /// the method is issued a <c>client_secret</c>.
    /// </summary>
    public bool RequiresClientSecret { get; init; }

    /// <summary>
    /// Gets a value indicating whether the method uses the <c>client_secret</c> as a key, as <c>client_secret_jwt</c>
    /// uses it as the HMAC key of the client's assertion. A secret issued to a client registered for such a method is
    /// stored so the server can read it back; a secret issued to a client registered for any other method is stored
    /// as a one-way verifier, which the server can only compare a presented secret with.
    /// </summary>
    // See ADR-1391.
    public bool UsesClientSecretAsKey { get; init; }
}
