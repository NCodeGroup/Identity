// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Contains the options that govern the root keys that owners bring, bound from the <c>OwnerRootKeys</c> child of the
/// <see cref="SecretProtectionOptions.DefaultSectionName"/> configuration section. The server calls the key management
/// service of an owner with the deployment's own identity, so the operator opts in to each key wrapper that an owner
/// may choose.
/// </summary>
// See ADR-1411.
[PublicAPI]
public sealed class OwnerRootKeyOptions
{
    /// <summary>
    /// Gets or sets the names of the key wrappers that an owner may choose for its root key. Each is registered and
    /// wraps under a key the caller names. It is empty by default, which lets no owner bring a key.
    /// </summary>
    public string[] Providers { get; set; } = [];

    /// <summary>
    /// Gets or sets how often an active owner root key is probed, and how long a replica reuses master key material
    /// that it unwrapped under an owner root key before it unwraps it again. It is between one minute and one day and a
    /// period a timer accepts. The default is one hour.
    /// </summary>
    public TimeSpan ValidationInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets how often an unavailable or removing owner root key is retried, how long a replica refuses an
    /// owner whose root key failed before it tries again, and how often the validation service looks for keys that are
    /// due. It is between one second and <see cref="ValidationInterval"/> and a period a timer accepts. The default is
    /// one minute.
    /// </summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromMinutes(1);
}
