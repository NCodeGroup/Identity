// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Contains the cached master keys of an owner.
/// </summary>
/// <param name="Active">The master key that protects new values.</param>
/// <param name="Keys">Every master key of the owner by its identifier, including the active master key.</param>
/// <param name="LoadedTimestamp">The <see cref="TimeProvider.GetTimestamp"/> value of the moment the entry was
/// loaded.</param>
// See ADR-1235 and ADR-1407.
internal sealed record MasterKeyRingEntry(
    MasterKey Active,
    FrozenDictionary<Guid, MasterKey> Keys,
    long LoadedTimestamp
)
{
    /// <summary>
    /// Determines whether the entry was loaded longer ago than a refresh interval, so that it has to be read again.
    /// </summary>
    /// <param name="timeProvider">The clock that produced <see cref="LoadedTimestamp"/>.</param>
    /// <param name="refreshInterval">How long an entry may be used.</param>
    /// <returns><see langword="true"/> when the entry is older than <paramref name="refreshInterval"/>.</returns>
    public bool IsStale(TimeProvider timeProvider, TimeSpan refreshInterval) =>
        timeProvider.GetElapsedTime(LoadedTimestamp) >= refreshInterval;

    /// <summary>
    /// Determines whether every specified master key is cached.
    /// </summary>
    /// <param name="keyIds">The identifiers of the master keys.</param>
    /// <returns><see langword="true"/> when every master key is cached.</returns>
    public bool Contains(IReadOnlySet<Guid> keyIds)
    {
        // A call that reads no secret requires no master key, and testing the count first spares that common case the
        // boxed enumerator that the interface would otherwise create.
        if (keyIds.Count == 0)
            return true;

        foreach (var keyId in keyIds)
        {
            if (!Keys.ContainsKey(keyId))
                return false;
        }

        return true;
    }
}
