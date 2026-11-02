// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Indicates that an instance supports a collection of <see cref="PersistedSecret"/> items.
/// </summary>
[PublicAPI]
public interface ISupportPersistedSecretCollection
{
    /// <summary>
    /// Gets the collection of <see cref="PersistedSecret"/> items.
    /// </summary>
    IReadOnlyCollection<PersistedSecret> Secrets { get; }
}
