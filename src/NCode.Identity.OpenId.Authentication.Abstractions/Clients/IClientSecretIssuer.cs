// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Issues a client's <c>client_secret</c> in the stored form that the client-authentication method it is registered for
/// needs, the one rule every site that issues a client secret applies: the management API, dynamic client
/// registration and the configuration seeds. A secret issued for a method whose
/// <see cref="ClientAuthenticationMethodDescriptor.UsesClientSecretAsKey"/> is set is stored so the server can read it
/// back; a secret issued for any other method, or for a method that is not registered, is stored only as a one-way
/// verifier. The form is fixed when the secret is issued.
/// </summary>
// See ADR-1391.
[PublicAPI]
public interface IClientSecretIssuer
{
    /// <summary>
    /// Generates a <c>client_secret</c> for a client registered for the specified method and returns it as the
    /// one-time <see cref="GeneratedSecret.SecretMaterial"/>, alongside the <see cref="PersistedSecret"/> that stores it
    /// in the form the method needs.
    /// </summary>
    /// <param name="protector">The <see cref="ISecretProtector"/> for the owner of the secret.</param>
    /// <param name="tokenEndpointAuthMethod">The client's effective <c>token_endpoint_auth_method</c>, or
    /// <see langword="null"/> when it is not known, which stores a one-way verifier.</param>
    /// <param name="request">The <see cref="GenerateSecretRequest"/> describing the shared secret to generate.</param>
    /// <returns>The <see cref="GeneratedSecret"/> with the protected stored form and the one-time secret to
    /// reveal.</returns>
    GeneratedSecret GenerateClientSecret(
        ISecretProtector protector,
        string? tokenEndpointAuthMethod,
        GenerateSecretRequest request
    );

    /// <summary>
    /// Stores a <strong>caller-supplied</strong> <c>client_secret</c> of a client registered for the specified method in
    /// the form the method needs. Nothing is revealed, because the caller already holds the value.
    /// </summary>
    /// <param name="protector">The <see cref="ISecretProtector"/> for the owner of the secret.</param>
    /// <param name="tokenEndpointAuthMethod">The client's effective <c>token_endpoint_auth_method</c>, or
    /// <see langword="null"/> when it is not known, which stores a one-way verifier.</param>
    /// <param name="request">The <see cref="ImportSharedSecretRequest"/> describing the secret's metadata.</param>
    /// <param name="secretValue">The UTF-8 octets of the opaque <c>client_secret</c>. Must not be empty.</param>
    /// <returns>The <see cref="PersistedSecret"/> with the protected stored form.</returns>
    /// <exception cref="ArgumentException"><paramref name="secretValue"/> is empty.</exception>
    PersistedSecret ImportClientSecret(
        ISecretProtector protector,
        string? tokenEndpointAuthMethod,
        ImportSharedSecretRequest request,
        ReadOnlySpan<byte> secretValue
    );

    /// <summary>
    /// Determines whether a stored secret's form can authenticate a client registered for the specified method: it is a
    /// shared secret, and when the method uses the secret as a key, the server can read it back. Whether it has expired
    /// is not judged.
    /// </summary>
    /// <param name="secret">The stored secret.</param>
    /// <param name="tokenEndpointAuthMethod">The client's effective <c>token_endpoint_auth_method</c>, or
    /// <see langword="null"/> when it is not known.</param>
    /// <returns><see langword="true"/> when the stored form can authenticate the client; otherwise,
    /// <see langword="false"/>.</returns>
    bool CanAuthenticate(PersistedSecret secret, string? tokenEndpointAuthMethod);
}
