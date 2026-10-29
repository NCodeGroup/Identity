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
/// Provides an abstraction for a store which persists <see cref="PersistedAuditRecord"/>: the audit history that an
/// operator queries after the logs that also carry it have rotated. Audit records are not tenant-scoped data: a record is
/// kept after its tenant is deleted, until its retention ends.
/// </summary>
// See ADR-0303.
[PublicAPI]
public interface IAuditRecordStore : IStore
{
    /// <summary>
    /// Adds, when the unit of work is saved, the records whose event identifiers the store does not hold yet. An event
    /// delivered again, or a batch retried after a save that committed but reported a failure, therefore adds nothing
    /// twice.
    /// </summary>
    /// <param name="records">The records to add; it must not be empty.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the count of records staged for addition.</returns>
    ValueTask<int> AddAsync(
        IReadOnlyCollection<PersistedAuditRecord> records,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets one page of a tenant's audit records that match a filter, newest first, using keyset (cursor) pagination,
    /// with each record's resources.
    /// </summary>
    /// <param name="filter">The tenant and the criteria the records must match.</param>
    /// <param name="page">The <see cref="PageRequest"/> describing the pagination window (cursor and limit).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page and the cursor for the next page.</returns>
    ValueTask<PagedResult<PersistedAuditRecord>> GetPageAsync(
        AuditRecordFilter filter,
        PageRequest page,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes, when the unit of work is saved, up to <paramref name="limit"/> of the records of events that occurred
    /// before <paramref name="time"/>, the end of the records' retention.
    /// </summary>
    /// <remarks>
    /// A caller prunes by repeating the removal, each time in a new unit of work, until fewer records than the limit are
    /// removed.
    /// </remarks>
    /// <param name="time">The earliest event time to keep.</param>
    /// <param name="limit">The greatest number of records to remove; it must be at least one.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the count of records staged for removal.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is less than one.</exception>
    ValueTask<int> RemoveBeforeAsync(
        DateTimeOffset time,
        int limit,
        CancellationToken cancellationToken
    );
}
