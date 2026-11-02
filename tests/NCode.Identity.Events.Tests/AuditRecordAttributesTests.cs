// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Events.Audit;
using Xunit;

namespace NCode.Identity.Events;

public class AuditRecordAttributesTests
{
    private sealed record NestedDetailsAuditEvent : AuditEvent, ISupportAuditDetails
    {
        public override string EventName => "test.nested";

        public override string EventCategory => "test";

        public void WriteAuditDetails(Utf8JsonWriter writer)
        {
            writer.WriteStartObject("a");
            writer.WriteNumber("b", 1);
            writer.WriteEndObject();
        }
    }

    #region Create Tests

    [Theory]
    [InlineData("audit.schema_version", "1.1")]
    [InlineData("audit.event_name", "test.detailed")]
    [InlineData("audit.actor.type", "client")]
    [InlineData("audit.request.source_ip", "203.0.113.5")]
    [InlineData("audit.resources", """[{"type":"session","id":"sid-1"}]""")]
    [InlineData("audit.error.status_code", 401L)]
    [InlineData("audit.details.token_type", "access_token")]
    public void Create_WhenEveryMemberIsSet_FlattensTheRecord(string key, object expected)
    {
        var attributes = AuditRecordAttributes.Create(TestDetailedAuditEvent.CreateSample());

        var actual = Assert.Single(attributes, x => x.Key == key);
        Assert.Equal(expected, actual.Value);
    }

    [Fact]
    public void Create_WhenEveryMemberIsSet_PrefixesEveryKey()
    {
        var attributes = AuditRecordAttributes.Create(TestDetailedAuditEvent.CreateSample());

        Assert.All(attributes, x => Assert.StartsWith($"{AuditRecordAttributes.Prefix}.", x.Key));
    }

    [Fact]
    public void Create_WhenOptionalMembersAreUnset_OmitsThem()
    {
        var attributes = AuditRecordAttributes.Create(
            new TestAuditEvent
            {
                EventSource = AuditEventSources.OpenId,
                Outcome = AuditOutcome.Success,
            }
        );

        Assert.DoesNotContain(attributes, x => x.Key == "audit.tenant_id");
        Assert.DoesNotContain(attributes, x => x.Key == "audit.request");
        Assert.DoesNotContain(attributes, x => x.Key == "audit.resources");
        Assert.DoesNotContain(attributes, x => x.Key == "audit.error");
        Assert.DoesNotContain(attributes, x => x.Key == "audit.details");
        Assert.Contains(attributes, x => x.Key == "audit.actor.type");
    }

    [Fact]
    public void Create_WhenDetailsHoldANestedObject_KeepsItAsRawJson()
    {
        var attributes = AuditRecordAttributes.Create(
            new NestedDetailsAuditEvent
            {
                EventSource = AuditEventSources.OpenId,
                Outcome = AuditOutcome.Success,
            }
        );

        var actual = Assert.Single(attributes, x => x.Key == "audit.details.a");
        Assert.Equal("""{"b":1}""", actual.Value);
        Assert.DoesNotContain(
            attributes,
            x => x.Key.StartsWith("audit.details.a.", StringComparison.Ordinal)
        );
    }

    #endregion
}
