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
using JetBrains.Annotations;

namespace NCode.Identity.Events.Audit;

/// <summary>
/// The base record for concrete resource-change audit events, supplying the common
/// <see cref="IResourceChangeAuditEvent"/> shape once so each derived event only declares its
/// <see cref="ResourceType"/>. The <see cref="IAuditEvent.EventName"/> is derived from the resource type and
/// change type (for example, <c>client.created</c>), the event category is
/// <see cref="AuditEventCategories.Management"/>, and the audit record lists the changed resource followed by the
/// resources that own it.
/// </summary>
/// <remarks>
/// <see cref="ResourceValues"/> carries a non-sensitive snapshot of the resource and must never include
/// secret material. The audit record's <c>details</c> carry the change type and, when present, the snapshot.
/// </remarks>
[PublicAPI]
public abstract record ResourceChangeAuditEvent
    : AuditEvent,
        IResourceChangeAuditEvent,
        ISupportAuditDetails
{
    /// <inheritdoc />
    public abstract string ResourceType { get; }

    /// <inheritdoc />
    public string? ResourceId { get; init; }

    /// <inheritdoc />
    public required string ChangeType { get; init; }

    /// <inheritdoc />
    public JsonElement? ResourceValues { get; init; }

    /// <inheritdoc />
    public override string EventName => $"{ResourceType}.{ChangeType}";

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Management;

    /// <inheritdoc />
    /// <remarks>
    /// Lists the changed resource first, then the <see cref="ParentResources"/>.
    /// </remarks>
    public override IReadOnlyList<AuditResource> Resources =>
        [new AuditResource(ResourceType, ResourceId), .. ParentResources];

    /// <summary>
    /// Gets the resources that own the changed resource, nearest first. A derived event overrides this when
    /// its resource belongs to another resource (for example, a client secret belongs to a client).
    /// </summary>
    protected virtual IEnumerable<AuditResource> ParentResources => [];

    /// <inheritdoc />
    /// <remarks>
    /// Writes the <c>change_type</c> and, when present, the <c>resource_values</c> snapshot.
    /// </remarks>
    public virtual void WriteAuditDetails(Utf8JsonWriter writer)
    {
        writer.WriteString("change_type", ChangeType);

        if (ResourceValues is { } values)
        {
            writer.WritePropertyName("resource_values");
            values.WriteTo(writer);
        }
    }
}
