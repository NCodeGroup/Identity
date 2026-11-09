// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Describes one key that a <see cref="IRemoteKeyProvider"/> lists from the key management service.
/// </summary>
// See ADR-1478.
[PublicAPI]
public sealed class RemoteKeyListing
{
    /// <summary>
    /// Gets the key management service's identifier for the key, which a <see cref="RemoteKeyReference"/> stores when
    /// a secret names the key.
    /// </summary>
    public required string KeyId { get; init; }

    /// <summary>
    /// Gets the name of the stable key in the key management service whose version this key is.
    /// </summary>
    public required string GroupName { get; init; }

    /// <summary>
    /// Gets when the key management service created the key.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets the identifier of the owner that the key's tags name, when the service keeps it.
    /// </summary>
    public string? OwnerId { get; init; }

    /// <summary>
    /// Gets the identifier of the secret that the key's tags name, when the service keeps it.
    /// </summary>
    public string? SecretId { get; init; }
}
