// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// The key type descriptor, the registered provider and the provider's algorithm row that a remote key request
/// resolves to, before the key management service is called.
/// </summary>
internal sealed class RemoteKeyResolution
{
    /// <summary>
    /// Gets the descriptor of the key type, which a key management service can hold.
    /// </summary>
    public required ISecretKeyTypeDescriptor Descriptor { get; init; }

    /// <summary>
    /// Gets the provider whose key management service holds the key.
    /// </summary>
    public required IRemoteKeyProvider Provider { get; init; }

    /// <summary>
    /// Gets the provider's row for the key, of any kind.
    /// </summary>
    public required RemoteKeyAlgorithm Algorithm { get; init; }
}
