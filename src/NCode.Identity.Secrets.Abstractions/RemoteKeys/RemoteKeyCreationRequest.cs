// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Describes a key for a <see cref="IRemoteKeyProvider"/> to create.
/// </summary>
// See ADR-1474.
[PublicAPI]
public sealed class RemoteKeyCreationRequest
{
    /// <summary>
    /// Gets the identifier of the owner, such as a tenant, whose key to create.
    /// </summary>
    public required string OwnerId { get; init; }

    /// <summary>
    /// Gets the identifier of the secret that the key belongs to.
    /// </summary>
    public required string SecretId { get; init; }

    /// <summary>
    /// Gets the row of the mapping vocabulary that selects the key's type, size and curve.
    /// </summary>
    public required RemoteSigningAlgorithm Algorithm { get; init; }

    /// <summary>
    /// Gets the name of the stable key in the key management service whose versions are one owner's successive keys
    /// for one algorithm and key specification. It is a valid key name for every provider.
    /// </summary>
    public required string GroupName { get; init; }
}
