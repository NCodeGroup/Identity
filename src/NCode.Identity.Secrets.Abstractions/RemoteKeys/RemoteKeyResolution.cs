// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// The remote key provider that reaches a key management service and the row of the mapping vocabulary that
/// applies to a key, as <see cref="IRemoteKeyResolver.Resolve"/> finds them.
/// </summary>
// See ADR-1479.
[PublicAPI]
public sealed class RemoteKeyResolution
{
    /// <summary>
    /// Gets the provider that reaches the key management service.
    /// </summary>
    public required IRemoteKeyProvider Provider { get; init; }

    /// <summary>
    /// Gets the row of the mapping vocabulary that applies to the key.
    /// </summary>
    public required RemoteKeyAlgorithm Algorithm { get; init; }

    /// <summary>
    /// Binds a key of the key management service to the provider and the row.
    /// </summary>
    /// <param name="keyId">The key management service's identifier for the key.</param>
    /// <returns>The binding of the key to the provider, its reference and the row.</returns>
    /// <exception cref="ArgumentException"><paramref name="keyId"/> is empty.</exception>
    public RemoteKeyBinding Bind(string keyId) =>
        new()
        {
            Provider = Provider,
            Reference = new RemoteKeyReference(Provider.Name, keyId),
            Algorithm = Algorithm,
        };
}
