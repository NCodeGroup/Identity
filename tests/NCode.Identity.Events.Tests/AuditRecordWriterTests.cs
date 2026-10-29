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
using System.Text;
using System.Text.Json;
using NCode.Identity.Events.Audit;
using Xunit;

namespace NCode.Identity.Events;

public class AuditRecordWriterTests
{
    private static UTF8Encoding StrictUtf8 { get; } =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    #region Write Tests

    [Fact]
    public void Write_WhenEveryMemberIsSet_WritesTheCanonicalRecord()
    {
        const string expected =
            """{"schema_version":"1.1","event_id":"01890a5d-ac96-774b-bcce-b302099a8057","event_time":"2026-10-28T14:03:12.345Z","event_name":"test.detailed","event_category":"authentication","event_source":"openid","outcome":"failure","severity":"warning","tenant_id":"tenant-1","subject_id":"subject-1","client_id":"client-1","actor":{"type":"client","id":"client-1"},"request":{"request_id":"req-1","trace_id":"4bf92f3577b34da6a3ce929d0e0e4736","span_id":"00f067aa0ba902b7","source_ip":"203.0.113.5","user_agent":"curl/8.0","http_method":"POST","path":"/oauth2/token","endpoint":"token_endpoint"},"resources":[{"type":"session","id":"sid-1"}],"error":{"code":"invalid_client","message":"bad secret","status_code":401},"details":{"token_type":"access_token"}}""";

        var output = new ArrayBufferWriter<byte>();
        AuditRecordWriter.Write(TestDetailedAuditEvent.CreateSample(), ref output);

        Assert.Equal(expected, StrictUtf8.GetString(output.WrittenSpan));
    }

    [Fact]
    public void Write_WhenTheDestinationIsAUtf8JsonWriter_WritesTheCanonicalRecord()
    {
        const string expected =
            """{"schema_version":"1.1","event_id":"01890a5d-ac96-774b-bcce-b302099a8057","event_time":"2026-10-28T14:03:12.345Z","event_name":"test.detailed","event_category":"authentication","event_source":"openid","outcome":"failure","severity":"warning","tenant_id":"tenant-1","subject_id":"subject-1","client_id":"client-1","actor":{"type":"client","id":"client-1"},"request":{"request_id":"req-1","trace_id":"4bf92f3577b34da6a3ce929d0e0e4736","span_id":"00f067aa0ba902b7","source_ip":"203.0.113.5","user_agent":"curl/8.0","http_method":"POST","path":"/oauth2/token","endpoint":"token_endpoint"},"resources":[{"type":"session","id":"sid-1"}],"error":{"code":"invalid_client","message":"bad secret","status_code":401},"details":{"token_type":"access_token"}}""";

        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output);
        AuditRecordWriter.Write(TestDetailedAuditEvent.CreateSample(), writer);
        writer.Flush();

        Assert.Equal(expected, StrictUtf8.GetString(output.WrittenSpan));
    }

    [Fact]
    public void Write_WhenOptionalMembersAreUnset_OmitsThem()
    {
        const string expected =
            """{"schema_version":"1.1","event_id":"00000000-0000-0000-0000-000000000000","event_time":"0001-01-01T00:00:00Z","event_name":"test.action","event_category":"test","event_source":"openid","outcome":"success","severity":"info","actor":{"type":"anonymous"}}""";

        var output = new ArrayBufferWriter<byte>();
        AuditRecordWriter.Write(
            new TestAuditEvent
            {
                EventSource = AuditEventSources.OpenId,
                Outcome = AuditOutcome.Success,
            },
            ref output
        );

        Assert.Equal(expected, StrictUtf8.GetString(output.WrittenSpan));
    }

    [Fact]
    public void Write_WhenTheTenantEnvironmentTypeIsSet_WritesItAfterTheTenantId()
    {
        const string expected = """"tenant_id":"t1","tenant_environment_type":"staging"""";

        var output = new ArrayBufferWriter<byte>();
        AuditRecordWriter.Write(
            new TestAuditEvent { TenantId = "t1", TenantEnvironmentType = "staging" },
            ref output
        );

        Assert.Contains(expected, StrictUtf8.GetString(output.WrittenSpan));
    }

    [Fact]
    public void Write_WhenTheEventHasNoDetails_OmitsTheDetailsObject()
    {
        var output = new ArrayBufferWriter<byte>();
        AuditRecordWriter.Write(
            TestDetailedAuditEvent.CreateSample() with
            {
                HasDetails = false,
            },
            ref output
        );

        Assert.DoesNotContain("\"details\"", StrictUtf8.GetString(output.WrittenSpan));
    }

    #endregion
}
