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

using System.Threading.Channels;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// Holds the audit records a replica has not yet written to the store, in a bounded queue. A record that arrives when the
/// queue is full is dropped and counted, so a store that is unreachable costs memory up to the bound and never blocks the
/// operation that was audited.
/// </summary>
// See ADR-0303.
internal sealed class AuditRecordQueue(IOptions<AuditRecordOptions> options)
{
    private long _droppedCount;

    private Channel<PersistedAuditRecord> Channel { get; } =
        System.Threading.Channels.Channel.CreateBounded<PersistedAuditRecord>(
            new BoundedChannelOptions(options.Value.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            }
        );

    /// <summary>
    /// Gets the number of records the queue holds.
    /// </summary>
    public int Count => Channel.Reader.Count;

    /// <summary>
    /// Adds a record to the queue, or drops and counts it when the queue is full.
    /// </summary>
    /// <param name="record">The record to add.</param>
    /// <returns><see langword="true"/> when the record was added; otherwise, <see langword="false"/>.</returns>
    public bool TryEnqueue(PersistedAuditRecord record)
    {
        if (Channel.Writer.TryWrite(record))
            return true;

        Interlocked.Increment(ref _droppedCount);
        return false;
    }

    /// <summary>
    /// Moves records from the queue to <paramref name="destination"/> until it holds <paramref name="limit"/> records or
    /// the queue is empty.
    /// </summary>
    /// <param name="destination">The list to add the records to.</param>
    /// <param name="limit">The greatest number of records the list holds afterwards.</param>
    public void Drain(List<PersistedAuditRecord> destination, int limit)
    {
        while (destination.Count < limit && Channel.Reader.TryRead(out var record))
            destination.Add(record);
    }

    /// <summary>
    /// Returns the number of records dropped since the last call, and starts counting again.
    /// </summary>
    public long TakeDroppedCount() => Interlocked.Exchange(ref _droppedCount, 0);
}
