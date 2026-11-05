// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Computes how long an owner has to wait before its master key may rotate again. A rotation before every replica has
/// loaded the active key protects nothing and only adds a retired key to rewrap and check, so the spacing is twice the
/// refresh interval.
/// </summary>
// See ADR-1407.
internal static class MasterKeyRotationSpacing
{
    /// <summary>
    /// Gets the time left before the owner's master key may rotate.
    /// </summary>
    /// <param name="rows">Every master key of the owner.</param>
    /// <param name="refreshInterval">The interval after which a replica reads the owner's master keys again.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The remaining delay, or <see langword="null"/> when the owner may rotate now.</returns>
    public static TimeSpan? GetRemaining(
        IReadOnlyList<PersistedMasterKey> rows,
        TimeSpan refreshInterval,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(rows);

        var spacing = 2 * refreshInterval;
        var lastRotation = rows.Max(row => row.RetiredWhen);

        return lastRotation is { } last && last + spacing > now ? last + spacing - now : null;
    }
}
