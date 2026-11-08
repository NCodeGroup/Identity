// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Binds a remote secret key to the provider that signs for it, the key's reference and the row of the mapping
/// vocabulary that selects how the key management service signs.
/// </summary>
// See ADR-1471.
// See ADR-1477.
[PublicAPI]
public sealed class RemoteKeyBinding
{
    /// <summary>
    /// Gets the provider that reaches the key management service holding the key.
    /// </summary>
    public required IRemoteKeyProvider Provider { get; init; }

    /// <summary>
    /// Gets the reference to the key in the key management service.
    /// </summary>
    public required RemoteKeyReference Reference { get; init; }

    /// <summary>
    /// Gets the row of the mapping vocabulary that the key was created for.
    /// </summary>
    public required RemoteKeyAlgorithm Algorithm { get; init; }
}
