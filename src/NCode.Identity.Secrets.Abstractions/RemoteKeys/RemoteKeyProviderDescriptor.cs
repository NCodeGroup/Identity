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
    /// Gets the provider name, compared ordinally. See <see cref="RemoteKeyProviderNames"/>.
    /// </summary>
    public required string Name { get; init; }
}
