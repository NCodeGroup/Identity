// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Describes the public half of a key for a <see cref="IRemoteKeyProvider"/> to read.
/// </summary>
// See ADR-1474.
[PublicAPI]
public sealed class RemotePublicKeyRequest
{
    /// <summary>
    /// Gets the key management service's identifier for the key whose public half to read.
    /// </summary>
    public required string KeyId { get; init; }

    /// <summary>
    /// Gets the row of the mapping vocabulary that selects the key's type, size and curve, which the public half
    /// must match.
    /// </summary>
    public required RemoteSigningAlgorithm Algorithm { get; init; }
}
