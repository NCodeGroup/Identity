// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Contains the options that govern the lifecycle of master keys, bound from the <c>MasterKeys</c> child of the
/// <see cref="SecretProtectionOptions.DefaultSectionName"/> configuration section. A master key rotates to a new
/// version after the rotation period, every replica reloads the keys of an owner after the refresh interval, and a
/// retired key is destroyed once nothing names it.
/// </summary>
// See ADR-1407.
[PublicAPI]
public sealed class MasterKeyOptions
{
    /// <summary>
    /// Gets or sets how long an active master key lives before the maintenance pass rotates it. It is at least one
    /// day. The default is 365 days.
    /// </summary>
    public TimeSpan RotationPeriod { get; set; } = TimeSpan.FromDays(365);

    /// <summary>
    /// Gets or sets how long a replica keeps the master keys of an owner in memory before it reads them again, which
    /// bounds how long a replica that missed a broadcast protects values under a retired key or keeps a destroyed key.
    /// It is between one second and one hour. The default is five minutes.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets how long a retired master key is kept before it can be destroyed, which covers in-flight
    /// protectors, replicas that have not reloaded and Data Protection payloads that a key protected just before it
    /// expired. It is at least twice <see cref="RefreshInterval"/>. The default is 30 days.
    /// </summary>
    public TimeSpan RetirementGrace { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Gets or sets how often the maintenance pass rotates overdue master keys and rewraps and destroys retired ones.
    /// It is at least one minute and a period a timer accepts. The default is one day.
    /// </summary>
    public TimeSpan MaintenanceInterval { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Gets or sets a value indicating whether a retired master key is destroyed once its grace has passed and no
    /// protected value names it. When <see langword="false"/>, retired keys are kept for good. The default is
    /// <see langword="true"/>.
    /// </summary>
    public bool DestroyRetired { get; set; } = true;
}
