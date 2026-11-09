// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using NCode.Identity.Secrets.Collections;

namespace NCode.Identity.Bff.Secrets;

/// <summary>
/// Holds the key rings of <c>Bff:KeyRings</c> that the BFF loads when the host starts, by name.
/// </summary>
// See ADR-1439.
// See ADR-1479.
internal sealed class BffKeyRings : IAsyncDisposable
{
    private ConcurrentDictionary<string, ISecretKeyCollectionProvider> KeyRings { get; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Adds a loaded key ring.
    /// </summary>
    /// <param name="name">The name of the key ring within <c>Bff:KeyRings</c>.</param>
    /// <param name="keyRing">The provider that holds the keys of the key ring.</param>
    /// <exception cref="InvalidOperationException">The key ring is already loaded.</exception>
    public void Add(string name, ISecretKeyCollectionProvider keyRing)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(keyRing);

        if (!KeyRings.TryAdd(name, keyRing))
        {
            throw new InvalidOperationException($"The key ring '{name}' is loaded more than once.");
        }
    }

    /// <summary>
    /// Gets a loaded key ring.
    /// </summary>
    /// <param name="name">The name of the key ring within <c>Bff:KeyRings</c>.</param>
    /// <returns>The provider that holds the keys of the key ring.</returns>
    /// <exception cref="InvalidOperationException">The key ring is not loaded.</exception>
    public ISecretKeyCollectionProvider Get(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (!KeyRings.TryGetValue(name, out var keyRing))
        {
            throw new InvalidOperationException(
                $"The key ring '{name}' is not loaded: the BFF loads the key rings of '{BffDefaults.ConfigurationSectionName}:KeyRings' when the host starts."
            );
        }

        return keyRing;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var keyRing in KeyRings.Values)
        {
            await keyRing.DisposeAsync();
        }
    }
}
