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

namespace NCode.Identity.Events.Audit;

/// <summary>
/// Flattens the canonical audit record of an <see cref="IAuditEvent"/> into <c>audit.*</c> key/value
/// pairs, so the log entry and the span event index the same fields the record declares.
/// </summary>
/// <remarks>
/// The record is flattened from the JSON that <see cref="AuditRecordWriter"/> writes, so the keys cannot
/// drift from the record. An object member recurses (<c>audit.actor.type</c>) and an array is kept as its
/// raw JSON text. The open <c>details</c> object is flattened one level deep (<c>audit.details.token_type</c>)
/// and a nested value in it is kept as raw JSON text, which bounds the number of index fields an event can
/// create. A <see langword="null"/> member is omitted.
/// </remarks>
internal static class AuditRecordAttributes
{
    /// <summary>
    /// The prefix of every key.
    /// </summary>
    public const string Prefix = "audit";

    /// <summary>
    /// Flattens the audit record of an audit event into <c>audit.*</c> key/value pairs, in the order of the
    /// record's fields.
    /// </summary>
    /// <param name="auditEvent">The audit event to flatten.</param>
    /// <returns>The key/value pairs of the audit record.</returns>
    public static List<KeyValuePair<string, object?>> Create(IAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var attributes = new List<KeyValuePair<string, object?>>();

        using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);
        using var writer = new Utf8JsonWriter(buffer);
        AuditRecordWriter.Write(auditEvent, writer);
        writer.Flush();

        using var lease = buffer.GetSpanLease(isSensitive: false);
        var record = JsonElement.Parse(lease.Span);

        foreach (var member in record.EnumerateObject())
        {
            if (
                member.Value.ValueKind == JsonValueKind.Object
                && string.Equals(member.Name, AuditRecordFields.Details, StringComparison.Ordinal)
            )
            {
                AddDetails(attributes, member.Value);
            }
            else
            {
                AddValue(attributes, $"{Prefix}.{member.Name}", member.Value);
            }
        }

        return attributes;
    }

    private static void AddDetails(
        List<KeyValuePair<string, object?>> attributes,
        JsonElement details
    )
    {
        foreach (var member in details.EnumerateObject())
        {
            var key = $"{Prefix}.{AuditRecordFields.Details}.{member.Name}";

            if (member.Value.ValueKind == JsonValueKind.Object)
            {
                Add(attributes, key, member.Value.GetRawText());
            }
            else
            {
                AddValue(attributes, key, member.Value);
            }
        }
    }

    private static void AddValue(
        List<KeyValuePair<string, object?>> attributes,
        string key,
        JsonElement value
    )
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var member in value.EnumerateObject())
                {
                    AddValue(attributes, $"{key}.{member.Name}", member.Value);
                }

                break;

            case JsonValueKind.Array:
                Add(attributes, key, value.GetRawText());
                break;

            case JsonValueKind.String:
                Add(attributes, key, value.GetString());
                break;

            case JsonValueKind.Number:
                if (value.TryGetInt64(out var integer))
                {
                    Add(attributes, key, integer);
                }
                else
                {
                    Add(attributes, key, value.GetDouble());
                }

                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                Add(attributes, key, value.GetBoolean());
                break;
        }
    }

    private static void Add(
        List<KeyValuePair<string, object?>> attributes,
        string key,
        object? value
    ) => attributes.Add(new KeyValuePair<string, object?>(key, value));
}
