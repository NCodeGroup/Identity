// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Holds what a <see cref="IRemoteKeyProvider"/> returns as soon as the key management service names the key it
/// created.
/// </summary>
// See ADR-1474.
[PublicAPI]
public sealed class RemoteKeyCreationResult
{
    /// <summary>
    /// Gets the key management service's identifier for the key, which the server stores in a
    /// <see cref="RemoteKeyReference"/>.
    /// </summary>
    public required string KeyId { get; init; }
}
