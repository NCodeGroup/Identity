// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Text.Json;
using NCode.Identity.Events.Audit;
using Xunit;

namespace NCode.Identity.Events;

public class ResourceChangeAuditEventTests
{
    private sealed record TestResourceChangeAuditEvent : ResourceChangeAuditEvent
    {
        public override string ResourceType => "widget";

        protected override IEnumerable<AuditResource> ParentResources =>
            [new AuditResource("gadget", "g-1")];
    }

    private static TestResourceChangeAuditEvent CreateSample() =>
        new()
        {
            EventSource = "management",
            Outcome = AuditOutcome.Success,
            ResourceId = "w-1",
            ChangeType = ResourceChangeTypes.Created,
            ResourceValues = JsonElement.Parse("""{"name":"x"}"""),
        };

    private static JsonElement ParseRecord(IAuditEvent auditEvent)
    {
        var output = new ArrayBufferWriter<byte>();
        AuditRecordWriter.Write(auditEvent, ref output);
        return JsonElement.Parse(output.WrittenSpan);
    }

    #region EventName Property Tests

    [Fact]
    public void EventName_WhenResourceTypeAndChangeTypeAreSet_JoinsThem()
    {
        var auditEvent = CreateSample();

        Assert.Equal("widget.created", auditEvent.EventName);
    }

    #endregion

    #region EventCategory Property Tests

    [Fact]
    public void EventCategory_Always_IsManagement()
    {
        var auditEvent = CreateSample();

        Assert.Equal(AuditEventCategories.Management, auditEvent.EventCategory);
    }

    #endregion

    #region Resources Property Tests

    [Fact]
    public void Resources_WhenParentResourcesExist_ListsTheTargetThenTheParents()
    {
        var auditEvent = CreateSample();

        Assert.Equal<AuditResource>(
            [new AuditResource("widget", "w-1"), new AuditResource("gadget", "g-1")],
            auditEvent.Resources
        );
    }

    #endregion

    #region WriteAuditDetails Tests

    [Fact]
    public void WriteAuditDetails_WhenResourceValuesAreSet_WritesTheChangeTypeAndTheSnapshot()
    {
        var auditEvent = CreateSample();

        var record = ParseRecord(auditEvent);

        Assert.Equal(
            """{"change_type":"created","resource_values":{"name":"x"}}""",
            record.GetProperty("details").GetRawText()
        );
    }

    [Fact]
    public void WriteAuditDetails_WhenResourceValuesAreNotSet_WritesOnlyTheChangeType()
    {
        var auditEvent = CreateSample() with { ResourceValues = null };

        var record = ParseRecord(auditEvent);

        Assert.Equal("""{"change_type":"created"}""", record.GetProperty("details").GetRawText());
    }

    #endregion
}
