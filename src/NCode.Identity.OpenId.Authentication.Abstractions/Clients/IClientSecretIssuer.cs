// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Issues and keeps a client's shared secrets, its <c>client_secret</c>, in the stored form that the
/// client-authentication method it is registered for needs: the one rule every site that issues a client secret or
/// changes a client's method applies, the management API, dynamic client registration and the configuration seeds. A
/// method whose <see cref="ClientAuthenticationMethodDescriptor.UsesClientSecretAsKey"/> is set needs a secret the
/// server can read back; every other method, and a method that is not registered, gets only a one-way verifier, which
/// the server can only compare a presented secret with. Every stored client names its method in its own settings
/// document, so a change to the server's or the tenant's <c>token_endpoint_auth_method</c> moves no stored client's
/// method, and a client's method changes only through its own settings.
/// </summary>
// See ADR-1391.
[PublicAPI]
public interface IClientSecretIssuer
{
    /// <summary>
    /// Resolves the client-authentication method a stored client is registered for: its effective
    /// <c>token_endpoint_auth_method</c>, the server's, the tenant's and the client's settings merged as the runtime
    /// merges them. When they cannot be merged, because the tenant's settings are absent or a settings document cannot
    /// be honored, the client authenticates with no method until they are fixed, and no method is resolved.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant that owns the client.</param>
    /// <param name="tenantSettings">The tenant's settings document as the store holds it, or <see langword="null"/>
    /// when the store holds none.</param>
    /// <param name="clientId">The identifier of the client.</param>
    /// <param name="clientSettings">The client's settings document.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous
    /// operation.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation, containing the client's
    /// effective <c>token_endpoint_auth_method</c>, or <see langword="null"/> when the settings cannot be merged.</returns>
    ValueTask<string?> GetTokenEndpointAuthMethodAsync(
        string tenantId,
        JsonElement? tenantSettings,
        string clientId,
        JsonElement clientSettings,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Determines whether a client registered for the specified method authenticates with a shared secret, so that it
    /// is issued a <c>client_secret</c>: the method's <see cref="ClientAuthenticationMethodDescriptor.RequiresClientSecret"/>.
    /// A method that is not registered requires none.
    /// </summary>
    /// <param name="tokenEndpointAuthMethod">The client's effective <c>token_endpoint_auth_method</c>.</param>
    /// <returns><see langword="true"/> when the method authenticates with a shared secret; otherwise,
    /// <see langword="false"/>.</returns>
    bool RequiresClientSecret(string tokenEndpointAuthMethod);

    /// <summary>
    /// Generates a <c>client_secret</c> for a client registered for the specified method and returns it as the
    /// one-time <see cref="GeneratedSecret.SecretMaterial"/>, alongside the <see cref="PersistedSecret"/> that stores it
    /// in the form the method needs.
    /// </summary>
    /// <param name="protector">The <see cref="ISecretProtector"/> for the owner of the secret.</param>
    /// <param name="tokenEndpointAuthMethod">The client's effective <c>token_endpoint_auth_method</c>, or
    /// <see langword="null"/> when it cannot be resolved; the secret of a method that cannot be resolved or is not
    /// registered is stored as a one-way verifier.</param>
    /// <param name="request">The <see cref="SharedSecretRequest"/> describing the secret, whose entropy defaults to
    /// <see cref="SharedSecretPolicy.MinimumEntropyBits"/>.</param>
    /// <returns>The <see cref="GeneratedSecret"/> with the protected stored form and the one-time secret to
    /// reveal.</returns>
    GeneratedSecret GenerateClientSecret(
        ISecretProtector protector,
        string? tokenEndpointAuthMethod,
        SharedSecretRequest request
    );

    /// <summary>
    /// Stores a <strong>caller-supplied</strong> <c>client_secret</c> of a client registered for the specified method in
    /// the form the method needs. Nothing is revealed, because the caller already holds the value.
    /// </summary>
    /// <param name="protector">The <see cref="ISecretProtector"/> for the owner of the secret.</param>
    /// <param name="tokenEndpointAuthMethod">The client's effective <c>token_endpoint_auth_method</c>, or
    /// <see langword="null"/> when it cannot be resolved; the secret of a method that cannot be resolved or is not
    /// registered is stored as a one-way verifier.</param>
    /// <param name="request">The <see cref="SharedSecretRequest"/> describing the secret's metadata.</param>
    /// <param name="secretValue">The UTF-8 octets of the opaque <c>client_secret</c>. Must not be empty.</param>
    /// <returns>The <see cref="PersistedSecret"/> with the protected stored form.</returns>
    /// <exception cref="ArgumentException"><paramref name="secretValue"/> is empty.</exception>
    PersistedSecret ImportClientSecret(
        ISecretProtector protector,
        string? tokenEndpointAuthMethod,
        SharedSecretRequest request,
        ReadOnlySpan<byte> secretValue
    );

    /// <summary>
    /// Brings a client's shared secrets into the form the method it is registered for needs, when its settings are
    /// updated. For a method that does not use the secret as a key, each shared secret stored as its octets is replaced
    /// by its one-way verifier, made with the scheme that fits the secret's <see cref="PersistedSecret.Origin"/>, so the
    /// client keeps its secret. When the method authenticates with a shared secret and none of the client's unexpired
    /// shared secrets can serve it, as a verifier cannot serve a method that keys from its secret, a new secret is
    /// issued in the method's form and replaces every other shared secret, none of which can authenticate the client.
    /// The client's other secrets are kept. When the method cannot be resolved, the secrets are left as they are,
    /// because a verifier cannot be turned back into its secret.
    /// </summary>
    /// <param name="protector">The <see cref="ISecretProtector"/> for the owner of the secrets.</param>
    /// <param name="tokenEndpointAuthMethod">The client's effective <c>token_endpoint_auth_method</c>, or
    /// <see langword="null"/> when it cannot be resolved.</param>
    /// <param name="secrets">The client's stored secrets.</param>
    /// <returns>The <see cref="ClientSecretReconciliation"/> with the client's secrets afterward, the secrets to remove
    /// from the store and to add to it, and the secret issued, if any, to reveal once.</returns>
    ClientSecretReconciliation ReconcileClientSecrets(
        ISecretProtector protector,
        string? tokenEndpointAuthMethod,
        IReadOnlyCollection<PersistedSecret> secrets
    );
}
