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
using JetBrains.Annotations;

namespace NCode.Identity.Events.Audit;

/// <summary>
/// Writes an <see cref="IAuditEvent"/> as the canonical audit record, a single JSON object whose field
/// names are declared by <see cref="AuditRecordFields"/>.
/// </summary>
/// <remarks>
/// Every sink that projects an audit event (the log entry, the span event, the metric attributes and any
/// future audit store) derives from this one shape, so they cannot drift apart. A member that is
/// <see langword="null"/>, and an empty <see cref="IAuditEvent.Resources"/> list, are omitted. An event
/// that implements <see cref="ISupportAuditDetails"/> contributes the open <c>details</c> object.
/// </remarks>
[PublicAPI]
public static class AuditRecordWriter
{
    /// <summary>
    /// The version of the audit record's schema. A field added to the record bumps the minor version.
    /// </summary>
    public const string SchemaVersion = "1.0";

    /// <summary>
    /// Writes the audit record of an audit event as one JSON object.
    /// </summary>
    /// <param name="writer">The writer to write the audit record to.</param>
    /// <param name="auditEvent">The audit event to write.</param>
    public static void Write(Utf8JsonWriter writer, IAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(auditEvent);

        writer.WriteStartObject();

        writer.WriteString(AuditRecordFields.SchemaVersion, SchemaVersion);
        writer.WriteString(AuditRecordFields.EventId, auditEvent.EventId);
        writer.WriteString(AuditRecordFields.EventTime, auditEvent.EventTime.UtcDateTime);
        writer.WriteString(AuditRecordFields.EventName, auditEvent.EventName);
        writer.WriteString(AuditRecordFields.EventCategory, auditEvent.EventCategory);
        writer.WriteString(AuditRecordFields.EventSource, auditEvent.EventSource);
        writer.WriteString(AuditRecordFields.Outcome, auditEvent.Outcome);
        writer.WriteString(AuditRecordFields.Severity, auditEvent.Severity);

        WriteIfNotNull(writer, AuditRecordFields.TenantId, auditEvent.TenantId);
        WriteIfNotNull(writer, AuditRecordFields.SubjectId, auditEvent.SubjectId);
        WriteIfNotNull(writer, AuditRecordFields.ClientId, auditEvent.ClientId);

        WriteActor(writer, auditEvent.Actor);

        if (auditEvent.Request is { } request)
        {
            WriteRequest(writer, request);
        }

        if (auditEvent.Resources is { Count: > 0 } resources)
        {
            WriteResources(writer, resources);
        }

        if (auditEvent.Error is { } error)
        {
            WriteError(writer, error);
        }

        if (auditEvent is ISupportAuditDetails supportAuditDetails)
        {
            writer.WriteStartObject(AuditRecordFields.Details);
            supportAuditDetails.WriteAuditDetails(writer);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes the audit record of an audit event as UTF-8 encoded JSON into a buffer writer.
    /// </summary>
    /// <param name="output">The buffer writer to write the UTF-8 encoded JSON of the audit record to.</param>
    /// <param name="auditEvent">The audit event to write.</param>
    public static void Write(IBufferWriter<byte> output, IAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(auditEvent);

        using var writer = new Utf8JsonWriter(output);
        Write(writer, auditEvent);
    }

    private static void WriteIfNotNull(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    private static void WriteActor(Utf8JsonWriter writer, AuditActor actor)
    {
        writer.WriteStartObject(AuditRecordFields.Actor);
        writer.WriteString(AuditRecordFields.ActorFields.Type, actor.Type);
        WriteIfNotNull(writer, AuditRecordFields.ActorFields.Id, actor.Id);
        writer.WriteEndObject();
    }

    private static void WriteRequest(Utf8JsonWriter writer, AuditRequest request)
    {
        writer.WriteStartObject(AuditRecordFields.Request);
        WriteIfNotNull(writer, AuditRecordFields.RequestFields.RequestId, request.RequestId);
        WriteIfNotNull(writer, AuditRecordFields.RequestFields.TraceId, request.TraceId);
        WriteIfNotNull(writer, AuditRecordFields.RequestFields.SpanId, request.SpanId);
        WriteIfNotNull(writer, AuditRecordFields.RequestFields.SourceIp, request.SourceIp);
        WriteIfNotNull(writer, AuditRecordFields.RequestFields.UserAgent, request.UserAgent);
        WriteIfNotNull(writer, AuditRecordFields.RequestFields.HttpMethod, request.HttpMethod);
        WriteIfNotNull(writer, AuditRecordFields.RequestFields.Path, request.Path);
        WriteIfNotNull(writer, AuditRecordFields.RequestFields.Endpoint, request.Endpoint);
        writer.WriteEndObject();
    }

    private static void WriteResources(
        Utf8JsonWriter writer,
        IReadOnlyList<AuditResource> resources
    )
    {
        writer.WriteStartArray(AuditRecordFields.Resources);
        foreach (var resource in resources)
        {
            writer.WriteStartObject();
            writer.WriteString(AuditRecordFields.ResourceFields.Type, resource.Type);
            WriteIfNotNull(writer, AuditRecordFields.ResourceFields.Id, resource.Id);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteError(Utf8JsonWriter writer, AuditError error)
    {
        writer.WriteStartObject(AuditRecordFields.Error);
        writer.WriteString(AuditRecordFields.ErrorFields.Code, error.Code);
        WriteIfNotNull(writer, AuditRecordFields.ErrorFields.Message, error.Message);
        if (error.StatusCode is { } statusCode)
        {
            writer.WriteNumber(AuditRecordFields.ErrorFields.StatusCode, statusCode);
        }

        writer.WriteEndObject();
    }
}
