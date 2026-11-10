// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Authentication.Usage;

/// <summary>
/// Provides the default implementation of <see cref="ITenantUsageRecorder"/>: it accumulates usage in memory until
/// <see cref="TenantUsageFlushService"/> drains it into the ledger.
/// </summary>
// See ADR-1199.
internal sealed class DefaultTenantUsageRecorder(TimeProvider timeProvider) : ITenantUsageRecorder
{
    private Lock Sync { get; } = new();
    private Dictionary<TenantUsageKey, long> Pending { get; set; } = [];

    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public void Record(string teamPath, string tenantId, string meter)
    {
        // A name the ledger cannot store would fail every flush of its batch, so it is refused here, at the caller.
        // See ADR-1426.
        ArgumentException.ThrowIfNullOrEmpty(teamPath);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            teamPath.Length,
            OpenIdMaxLengths.TeamPath,
            nameof(teamPath)
        );
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ArgumentException.ThrowIfNullOrEmpty(meter);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            meter.Length,
            OpenIdMaxLengths.UsageMeter,
            nameof(meter)
        );

        var day = DateOnly.FromDateTime(TimeProvider.GetUtcNow().UtcDateTime);
        var key = new TenantUsageKey(teamPath, tenantId, day, meter);
        lock (Sync)
        {
            Pending[key] = Pending.GetValueOrDefault(key) + 1;
        }
    }

    /// <summary>
    /// Takes the usage recorded since the last drain.
    /// </summary>
    /// <returns>The usage recorded since the last drain.</returns>
    public IReadOnlyDictionary<TenantUsageKey, long> Drain()
    {
        lock (Sync)
        {
            var drained = Pending;
            Pending = [];
            return drained;
        }
    }
}
