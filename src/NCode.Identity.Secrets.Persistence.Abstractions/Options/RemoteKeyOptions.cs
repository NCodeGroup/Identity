// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Contains the options for the keys that the server holds in a key management service, bound from the
/// <c>RemoteKeys</c> child of the <see cref="SecretProtectionOptions.DefaultSectionName"/> configuration section.
/// The deployment name scopes the keys the server creates, and a background sweep destroys the keys that no stored
/// secret names.
/// </summary>
// See ADR-1478.
[PublicAPI]
public sealed class RemoteKeyOptions
{
    /// <summary>
    /// Gets or sets the name of this deployment, which every key the server creates in a key management service
    /// carries and its group name hashes, so deployments that share a service scope never share or sweep each
    /// other's keys. It is lowercase letters and digits in hyphen-separated groups, at most 40 characters. It is
    /// required when a remote key provider that creates keys is registered.
    /// </summary>
    public string? Deployment { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the background sweep destroys the keys that no stored secret names.
    /// The default is <see langword="true"/>.
    /// </summary>
    public bool SweepEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets how often one replica sweeps every provider for keys that no stored secret names. The default is
    /// one day.
    /// </summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Gets or sets how old a key must be before a sweep destroys it, so a key whose creation is in flight is never
    /// swept. It is between one hour and 365 days. The default is one day.
    /// </summary>
    public TimeSpan SweepGracePeriod { get; set; } = TimeSpan.FromDays(1);
}
