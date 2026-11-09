// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Describes the keys for a <see cref="IRemoteKeyProvider"/> to list.
/// </summary>
// See ADR-1478.
[PublicAPI]
public sealed class RemoteKeyListRequest
{
    /// <summary>
    /// Gets the name of the deployment whose keys to list.
    /// </summary>
    public required string Deployment { get; init; }
}
