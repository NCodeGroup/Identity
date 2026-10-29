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

namespace NCode.Identity.Events;

/// <summary>
/// A concrete audit event used by tests that sets every member of the audit record, including the
/// event-specific <c>details</c>.
/// </summary>
internal sealed record TestDetailedAuditEvent : AuditEvent, ISupportAuditDetails
{
    public override string EventName => "test.detailed";

    public override string EventCategory => AuditEventCategories.Authentication;

    public override IReadOnlyList<AuditResource> Resources =>
        [new AuditResource(AuditResourceTypes.Session, "sid-1")];

    public bool HasDetails { get; init; } = true;

    public bool HasAuditDetails => HasDetails;

    public void WriteAuditDetails(Utf8JsonWriter writer)
    {
        writer.WriteString("token_type", "access_token");
    }

    public static TestDetailedAuditEvent CreateSample() =>
        new()
        {
            EventId = Guid.Parse("01890a5d-ac96-774b-bcce-b302099a8057"),
            EventTime = new DateTimeOffset(2026, 10, 28, 14, 3, 12, 345, TimeSpan.Zero),
            EventSource = AuditEventSources.OpenId,
            Outcome = AuditOutcome.Failure,
            TenantId = "tenant-1",
            SubjectId = "subject-1",
            ClientId = "client-1",
            Actor = AuditActor.Client("client-1"),
            Request = new AuditRequest
            {
                RequestId = "req-1",
                TraceId = "4bf92f3577b34da6a3ce929d0e0e4736",
                SpanId = "00f067aa0ba902b7",
                SourceIp = "203.0.113.5",
                UserAgent = "curl/8.0",
                HttpMethod = "POST",
                Path = "/oauth2/token",
                Endpoint = "token_endpoint",
            },
            Error = new AuditError("invalid_client", "bad secret", 401),
        };
}
