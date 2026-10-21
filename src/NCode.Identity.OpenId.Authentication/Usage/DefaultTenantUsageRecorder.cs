#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

namespace NCode.Identity.OpenId.Authentication.Usage;

/// <summary>
/// Provides the default implementation of <see cref="ITenantUsageRecorder"/>: it accumulates usage in memory until
/// <see cref="TenantUsageFlushService"/> drains it into the ledger (ADR-0199).
/// </summary>
internal sealed class DefaultTenantUsageRecorder(TimeProvider timeProvider) : ITenantUsageRecorder
{
    private readonly Lock _sync = new();
    private Dictionary<TenantUsageKey, long> _pending = [];

    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public void Record(string tenantId, string meter)
    {
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

    /// <summary>
    /// Returns usage that could not be added to the ledger, so the next flush adds it.
    /// </summary>
    /// <param name="usage">The usage to return.</param>
    public void Restore(IReadOnlyDictionary<TenantUsageKey, long> usage)
    {
        lock (_sync)
        {
            foreach (var (key, count) in usage)
            {
                _pending[key] = _pending.GetValueOrDefault(key) + count;
            }
        }
    }
}
