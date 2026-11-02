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
    private readonly Lock _sync = new();
    private Dictionary<TenantUsageKey, long> _pending = [];

    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public void Record(string tenantId, string meter)
    {
        // A name the ledger cannot store would fail every flush of its batch, so it is refused here, at the caller.
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ArgumentException.ThrowIfNullOrEmpty(meter);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            meter.Length,
            OpenIdMaxLengths.UsageMeter,
            nameof(meter)
        );

        var day = DateOnly.FromDateTime(TimeProvider.GetUtcNow().UtcDateTime);
        var key = new TenantUsageKey(tenantId, day, meter);
        lock (_sync)
        {
            _pending[key] = _pending.GetValueOrDefault(key) + 1;
        }
    }

    /// <summary>
    /// Takes the usage recorded since the last drain.
    /// </summary>
    /// <returns>The usage recorded since the last drain.</returns>
    public IReadOnlyDictionary<TenantUsageKey, long> Drain()
    {
        lock (_sync)
        {
            var drained = _pending;
            _pending = [];
            return drained;
        }
    }
}
