// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Describes a remote key provider without constructing it.
/// </summary>
// See ADR-1474.
[PublicAPI]
public sealed class RemoteKeyProviderDescriptor
{
    /// <summary>
    /// Gets a value indicating whether the provider creates and lists keys. A provider without a key-creation
    /// location reads, signs, decrypts, agrees with and destroys the keys it is given by their identifiers, and
    /// creates and lists none. The default is <see langword="true"/>.
    /// </summary>
    public bool CreatesKeys { get; init; } = true;

    /// <summary>
    /// Gets the provider name, compared ordinally. See <see cref="RemoteKeyProviderNames"/>.
    /// </summary>
    public required string Name { get; init; }
}
