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
/// tenant's usage that replicas add to. Usage is not tenant-scoped data: it is kept after its tenant is
/// deleted, so the platform can still bill the tenant's last days.
/// </summary>
// See ADR-0199 and ADR-0209.
[PublicAPI]
public interface ITenantUsageStore : IStore
{
    /// <summary>
    /// Adds a batch of usage that one replica recorded. Each entry's count is added to its tenant's usage of the meter
    /// on the day, and the day's entry is created when it does not exist. The batch's number is recorded as the
    /// replica's last added batch. A replica numbers its batches from one, in the order it adds them, so a batch
    /// numbered at or below the replica's last added batch was added before, and adding it again changes nothing. A
    /// replica therefore retries a batch whose save failed under the same number, and its counts are added once even
    /// when that save committed. The entries are read together, so a batch costs one read per day it spans. The change
    /// is committed by the unit of work. A concurrent writer to the same entry, or to the replica's last added batch,
    /// makes the commit fail, and the caller retries the batch with a new unit of work.
    /// </summary>
    /// <param name="replicaId">The identifier the replica drew when it started.</param>
    /// <param name="batchNumber">The number of the batch, from one, in the order the replica adds its batches.</param>
    /// <param name="usage">The usage to add; it must not be empty.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask AddAsync(
        Guid replicaId,
        long batchNumber,
        IReadOnlyCollection<PersistedTenantUsage> usage,
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
    /// Removes, when the unit of work is saved, up to <paramref name="limit"/> of the usage entries counted before
    /// <paramref name="day"/>, the end of the ledger's retention, and the last added batch of every replica whose
    /// latest usage day is before it.
    /// </summary>
    /// <remarks>
    /// A caller prunes by repeating the removal, each time in a new unit of work, until fewer entries than the limit
    /// are removed. Two callers that remove the same entries conflict: the later commit fails as a lost concurrency
    /// race, and that caller reads the next batch.
    /// </remarks>
    /// <param name="day">The first UTC calendar day to keep.</param>
    /// <param name="limit">The greatest number of usage entries to remove; it must be at least one.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the count of usage entries staged for removal.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is less than one.</exception>
    ValueTask<int> RemoveBeforeAsync(DateOnly day, int limit, CancellationToken cancellationToken);
}
