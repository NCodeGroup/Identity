// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Reaches one key management service to create keys whose private half never leaves it, to read their public half,
/// to sign with them, and to destroy them.
/// </summary>
/// <remarks>
/// A constructor does no I/O, reads no options value and creates no client, so constructing a provider is cheap and
/// code that reaches a provider through <see cref="IRemoteKeyProviderCatalog"/> does not touch the key management
/// service.
/// <para>
/// A response that fails an integrity check, such as a checksum mismatch or a response that names another key, key
/// version or algorithm than requested, throws <see cref="IOException"/> because the call can be retried. A key
/// that the provider refuses, such as a key with the wrong usage, specification, algorithms or key type or a missing
/// or malformed public key or created identifier, throws <see cref="CryptographicException"/>.
/// </para>
/// </remarks>
// See ADR-1474.
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
    /// <see cref="RemoteKeyCreationRequest.GroupName"/>. The call to the key management service is made once and is
    /// never retried.
    /// </summary>
    /// <param name="request">The key to create.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// The key management service's identifier for the key, as soon as the service names it and before any other
    /// operation can fail.
    /// </returns>
    ValueTask<RemoteKeyCreationResult> CreateKeyAsync(
        RemoteKeyCreationRequest request,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Reads the public half of a key from the key management service and checks that the key is usable for the
    /// request's algorithm.
    /// </summary>
    /// <param name="request">The key to read and the algorithm it must match.</param>
    /// <param name="subjectPublicKeyInfo">The writer that receives the DER-encoded <c>SubjectPublicKeyInfo</c>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the public half is written.</returns>
    ValueTask GetPublicKeyAsync(
        RemotePublicKeyRequest request,
        IBufferWriter<byte> subjectPublicKeyInfo,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Asks the key management service to sign the data of the request with the request's key, and writes the
    /// signature exactly as the service returns it into the caller's buffer.
    /// </summary>
    /// <param name="request">The data to sign and the key to sign with.</param>
    /// <param name="signature">The buffer that receives the signature.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of bytes written to <paramref name="signature"/>.</returns>
    /// <exception cref="CryptographicException">The signature does not fit in <paramref name="signature"/>.</exception>
    ValueTask<int> SignAsync(
        RemoteSignatureRequest request,
        Memory<byte> signature,
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
