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

using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Json;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Auditing;

public class AuditRecordQueueTests
{
    private static PersistedAuditRecord CreateRecord() =>
        new()
        {
            EventId = Guid.CreateVersion7(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventName = "token.issued",
            EventCategory = "token",
            Outcome = "success",
            Severity = "info",
            ActorType = "client",
            Resources = [],
            Record = JsonElements.EmptyObject,
        };

    private static AuditRecordQueue CreateQueue(int capacity) =>
        new(Options.Create(new AuditRecordOptions { QueueCapacity = capacity }));

    #region TryEnqueue Tests

    [Fact]
    public void TryEnqueue_WhenTheQueueIsFull_DropsAndCountsTheRecord()
    {
        var queue = CreateQueue(capacity: 2);

        Assert.True(queue.TryEnqueue(CreateRecord()));
        Assert.True(queue.TryEnqueue(CreateRecord()));
        Assert.False(queue.TryEnqueue(CreateRecord()));
        Assert.False(queue.TryEnqueue(CreateRecord()));

        Assert.Equal(2, queue.Count);
        Assert.Equal(2, queue.TakeDroppedCount());
        Assert.Equal(0, queue.TakeDroppedCount());
    }

    #endregion

    #region Drain Tests

    [Fact]
    public void Drain_MovesRecordsInOrderUpToTheLimit()
    {
        var queue = CreateQueue(capacity: 10);
        var records = Enumerable.Range(0, 3).Select(_ => CreateRecord()).ToList();
        foreach (var record in records)
            queue.TryEnqueue(record);
        var destination = new List<PersistedAuditRecord> { records[0] };

        queue.Drain(destination, limit: 3);

        Assert.Equal([records[0], records[0], records[1]], destination);
        Assert.Equal(1, queue.Count);
    }

    #endregion
}
