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
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Auditing;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Auditing;

public class AuditRecordHandlerTests
{
    private sealed record TestAuditEvent : AuditEvent
    {
        public override string EventName => "token.issued";

        public override string EventCategory => AuditEventCategories.Token;
    }

    private static TestAuditEvent CreateEvent() =>
        new()
        {
            EventId = Guid.CreateVersion7(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.OpenId,
            Outcome = AuditOutcome.Success,
        };

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenEnabled_QueuesTheEventsRecord()
    {
        var options = Options.Create(new AuditRecordOptions());
        var queue = new AuditRecordQueue(options);
        var handler = new AuditRecordHandler(options, queue);
        var auditEvent = CreateEvent();

        await handler.HandleAsync(auditEvent, CancellationToken.None);

        var drained =
            new List<NCode.Identity.OpenId.Persistence.DataContracts.PersistedAuditRecord>();
        queue.Drain(drained, limit: 10);
        Assert.Equal(auditEvent.EventId, Assert.Single(drained).EventId);
    }

    [Fact]
    public async Task HandleAsync_WhenDisabled_QueuesNothing()
    {
        var options = Options.Create(new AuditRecordOptions { Enabled = false });
        var queue = new AuditRecordQueue(options);
        var handler = new AuditRecordHandler(options, queue);

        await handler.HandleAsync(CreateEvent(), CancellationToken.None);

        Assert.Equal(0, queue.Count);
    }

    #endregion
}
