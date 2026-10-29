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
using NCode.Buffers;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// Maps an <see cref="IAuditEvent"/> to the <see cref="PersistedAuditRecord"/> the audit store keeps: the envelope fields
/// it indexes and the whole record that <see cref="AuditRecordWriter"/> writes. An envelope value longer than its column
/// is not indexed, so an optional field is <see langword="null"/> and a required one keeps its leading characters, while
/// the record keeps it whole.
/// </summary>
// See ADR-0303.
internal static class AuditRecordMapper
{
    /// <summary>
    /// Maps an audit event to the record the audit store keeps.
    /// </summary>
    /// <param name="auditEvent">The audit event.</param>
    /// <returns>The record.</returns>
    public static PersistedAuditRecord Map(IAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            AuditRecordWriter.Write(auditEvent, writer);
        }

        using var lease = buffer.GetSpanLease(isSensitive: false);

        var request = auditEvent.Request;
        return new PersistedAuditRecord
        {
            EventId = auditEvent.EventId,
            EventTime = auditEvent.EventTime,
            EventName = Truncate(auditEvent.EventName, OpenIdMaxLengths.AuditName),
            EventCategory = Truncate(auditEvent.EventCategory, OpenIdMaxLengths.AuditName),
            Outcome = Truncate(auditEvent.Outcome, OpenIdMaxLengths.AuditName),
            Severity = Truncate(auditEvent.Severity, OpenIdMaxLengths.AuditName),
            TenantId = Bounded(auditEvent.TenantId, MaxLengths.ResourceId),
            SubjectId = Bounded(auditEvent.SubjectId, OpenIdMaxLengths.SubjectId),
            ClientId = Bounded(auditEvent.ClientId, MaxLengths.ResourceId),
            ActorType = Truncate(auditEvent.Actor.Type, OpenIdMaxLengths.AuditName),
            ActorId = Bounded(auditEvent.Actor.Id, MaxLengths.ResourceId),
            TraceId = Bounded(request?.TraceId, OpenIdMaxLengths.AuditTraceId),
            SourceIp = Bounded(request?.SourceIp, OpenIdMaxLengths.AuditSourceIp),
            Resources =
            [
                .. auditEvent.Resources.Select(resource => new PersistedAuditResource
                {
                    Type = Truncate(resource.Type, OpenIdMaxLengths.AuditName),
                    Id = Bounded(resource.Id, MaxLengths.ResourceId),
                }),
            ],
            Record = JsonElement.Parse(lease.Span),
        };
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string? Bounded(string? value, int maxLength) =>
        value is not null && value.Length <= maxLength ? value : null;
}
