// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Holds what a <see cref="IRemoteKeyProvider"/> returns after it creates a key.
/// </summary>
// See ADR-1471.
[PublicAPI]
public sealed class RemoteKeyCreationResult
{
    /// <summary>
    /// Gets the key management service's identifier for the key, which the server stores in a
    /// <see cref="RemoteKeyReference"/>.
    /// </summary>
    public required string KeyId { get; init; }

    /// <summary>
    /// Gets the DER-encoded <c>SubjectPublicKeyInfo</c> of the key's public half, as the key management service
    /// returns it.
    /// </summary>
    public required ReadOnlyMemory<byte> SubjectPublicKeyInfo { get; init; }
}
