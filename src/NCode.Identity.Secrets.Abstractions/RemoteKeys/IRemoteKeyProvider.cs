// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Reaches one key management service to create keys whose private half never leaves it, to sign with them, and to
/// destroy them.
/// </summary>
// See ADR-1471.
[PublicAPI]
public interface IRemoteKeyProvider
{
    /// <summary>
    /// Gets the name of the provider, from <see cref="RemoteKeyProviderNames"/> or a provider's own name. One name
    /// selects one key management service.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Creates a key in the key management service, as a new version of the key for the request's
    /// <see cref="RemoteKeyCreationRequest.GroupName"/>, and reads its public half.
    /// </summary>
    /// <param name="request">The key to create.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The key management service's identifier for the key and its <c>SubjectPublicKeyInfo</c>.</returns>
    ValueTask<RemoteKeyCreationResult> CreateKeyAsync(
        RemoteKeyCreationRequest request,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Asks the key management service to sign the data of the request with the request's key.
    /// </summary>
    /// <param name="request">The data to sign and the key to sign with.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The signature exactly as the key management service returns it.</returns>
    ValueTask<byte[]> SignAsync(
        RemoteSignatureRequest request,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Destroys the key in the key management service. Completes without error for a key that is already destroyed,
    /// disabled, scheduled for deletion or missing.
    /// </summary>
    /// <param name="keyId">The key management service's identifier for the key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the key is destroyed.</returns>
    ValueTask DestroyKeyAsync(string keyId, CancellationToken cancellationToken);
}
