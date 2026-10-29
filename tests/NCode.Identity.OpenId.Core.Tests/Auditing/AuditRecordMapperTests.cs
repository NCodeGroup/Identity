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

using System.Text.Json;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Auditing;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Auditing;

public class AuditRecordMapperTests
{
    private sealed record TestAuditEvent : AuditEvent
    {
        public string Name { get; init; } = "client.updated";

        public IReadOnlyList<AuditResource> TestResources { get; init; } = [];

        public override string EventName => Name;

        public override string EventCategory => AuditEventCategories.Management;

        public override IReadOnlyList<AuditResource> Resources => TestResources;
    }

    private static TestAuditEvent CreateEvent() =>
        new()
        {
            EventId = Guid.CreateVersion7(),
            EventTime = new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero),
            EventSource = AuditEventSources.OpenId,
            Outcome = AuditOutcome.Success,
            TenantId = "tenant-1",
            SubjectId = "user-1",
            ClientId = "client-1",
            Actor = AuditActor.Administrator("admin-1"),
            Request = new AuditRequest
            {
                TraceId = "0af7651916cd43dd8448eb211c80319c",
                SourceIp = "203.0.113.7",
            },
            TestResources =
            [
                new AuditResource("client", "client-1"),
                new AuditResource("tenant", "tenant-1"),
            ],
        };

    #region Map Tests

    [Fact]
    public void Map_CopiesTheEnvelopeAndWritesTheWholeRecord()
    {
        var auditEvent = CreateEvent();

        var record = AuditRecordMapper.Map(auditEvent);

        Assert.Equal(auditEvent.EventId, record.EventId);
        Assert.Equal(auditEvent.EventTime, record.EventTime);
        Assert.Equal("client.updated", record.EventName);
        Assert.Equal(AuditEventCategories.Management, record.EventCategory);
        Assert.Equal(AuditOutcome.Success, record.Outcome);
        Assert.Equal(auditEvent.Severity, record.Severity);
        Assert.Equal("tenant-1", record.TenantId);
        Assert.Equal("user-1", record.SubjectId);
        Assert.Equal("client-1", record.ClientId);
        Assert.Equal(AuditActorTypes.Administrator, record.ActorType);
        Assert.Equal("admin-1", record.ActorId);
        Assert.Equal("0af7651916cd43dd8448eb211c80319c", record.TraceId);
        Assert.Equal("203.0.113.7", record.SourceIp);
        Assert.Equal(
            [("client", "client-1"), ("tenant", "tenant-1")],
            record.Resources.Select(resource => (resource.Type, resource.Id!))
        );

        var json = JsonElement.Parse(record.Record);
        Assert.Equal(auditEvent.EventId, json.GetProperty(AuditRecordFields.EventId).GetGuid());
        Assert.Equal("client.updated", json.GetProperty(AuditRecordFields.EventName).GetString());
    }

    [Fact]
    public void Map_WhenAnEnvelopeValueIsLongerThanItsColumn_IndexesNoneOfItButKeepsItInTheRecord()
    {
        var longName = new string('n', 150);
        var longClientId = new string('c', 400);
        var auditEvent = CreateEvent() with { Name = longName, ClientId = longClientId };

        var record = AuditRecordMapper.Map(auditEvent);

        Assert.Equal(longName[..100], record.EventName);
        Assert.Null(record.ClientId);
        var json = JsonElement.Parse(record.Record);
        Assert.Equal(longName, json.GetProperty(AuditRecordFields.EventName).GetString());
        Assert.Equal(longClientId, json.GetProperty(AuditRecordFields.ClientId).GetString());
    }

    [Fact]
    public void Map_WithoutARequest_LeavesTheTraceAndSourceEmpty()
    {
        var auditEvent = CreateEvent() with { Request = null };

        var record = AuditRecordMapper.Map(auditEvent);

        Assert.Null(record.TraceId);
        Assert.Null(record.SourceIp);
    }

    #endregion
}
