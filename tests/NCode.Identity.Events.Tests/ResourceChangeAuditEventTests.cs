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
            ResourceValues = JsonDocument.Parse("""{"name":"x"}""").RootElement,
        };

    private static JsonDocument ParseRecord(IAuditEvent auditEvent)
    {
        var output = new ArrayBufferWriter<byte>();
        AuditRecordWriter.Write(output, auditEvent);
        return JsonDocument.Parse(output.WrittenMemory);
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

        using var record = ParseRecord(auditEvent);

        Assert.Equal(
            """{"change_type":"created","resource_values":{"name":"x"}}""",
            record.RootElement.GetProperty("details").GetRawText()
        );
    }

    [Fact]
    public void WriteAuditDetails_WhenResourceValuesAreNotSet_WritesOnlyTheChangeType()
    {
        var auditEvent = CreateSample() with { ResourceValues = null };

        using var record = ParseRecord(auditEvent);

        Assert.Equal(
            """{"change_type":"created"}""",
            record.RootElement.GetProperty("details").GetRawText()
        );
    }

    #endregion
}
