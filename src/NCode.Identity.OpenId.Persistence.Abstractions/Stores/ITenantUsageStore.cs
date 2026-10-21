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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Provides an abstraction for a store which persists <see cref="PersistedTenantUsage"/>: the daily ledger of each
/// tenant's usage that replicas add to (ADR-0199). Usage is not tenant-scoped data: it is kept after its tenant is
/// deleted, so the platform can still bill the tenant's last days.
/// </summary>
[PublicAPI]
public interface ITenantUsageStore : IStore
{
    /// <summary>
    /// Adds <paramref name="count"/> to a tenant's usage of a meter on a day, creating the day's entry when it does not
    /// exist. The change is committed by the unit of work; a concurrent writer to the same entry makes the commit fail,
    /// and the caller retries with a new unit of work.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <param name="day">The UTC calendar day.</param>
    /// <param name="meter">The name of the meter.</param>
    /// <param name="count">The count to add.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask AddAsync(
        string tenantId,
        DateOnly day,
        string meter,
        long count,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Lists a tenant's usage of every meter on the days from <paramref name="firstDay"/> to <paramref name="lastDay"/>,
    /// both inclusive, ordered by day and then by meter.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <param name="firstDay">The first UTC calendar day to list.</param>
    /// <param name="lastDay">The last UTC calendar day to list.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the usage.</returns>
    ValueTask<IReadOnlyList<PersistedTenantUsage>> ListAsync(
        string tenantId,
        DateOnly firstDay,
        DateOnly lastDay,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes every tenant's usage counted before <paramref name="day"/>, the end of the ledger's retention.
    /// </summary>
    /// <param name="day">The first UTC calendar day to keep.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the count of entries removed.</returns>
    ValueTask<int> RemoveBeforeAsync(DateOnly day, CancellationToken cancellationToken);
}
