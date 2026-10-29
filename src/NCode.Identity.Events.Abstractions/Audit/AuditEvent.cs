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

using JetBrains.Annotations;

namespace NCode.Identity.Events.Audit;

/// <summary>
/// The base record for concrete audit events, supplying the common <see cref="IAuditEvent"/> envelope
/// once so each derived event only declares what is specific to it.
/// </summary>
/// <remarks>
/// This type is a deliberate extension point; derive a concrete audit event from it, override
/// <see cref="EventName"/> and <see cref="EventCategory"/>, set <see cref="EventSource"/>, override
/// <see cref="Resources"/> for the entities acted on, and implement <see cref="ISupportAuditDetails"/>
/// for event-specific fields. The schema version of the serialized record is
/// <see cref="AuditRecordWriter.SchemaVersion"/>. An audit event carries identifiers and metadata
/// only, never secret material.
/// </remarks>
[PublicAPI]
public abstract record AuditEvent : IAuditEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; }

    /// <inheritdoc />
    public DateTimeOffset EventTime { get; init; }

    /// <inheritdoc />
    public abstract string EventName { get; }

    /// <inheritdoc />
    public abstract string EventCategory { get; }

    /// <inheritdoc />
    public required string EventSource { get; init; }

    /// <inheritdoc />
    public virtual string Severity =>
        string.Equals(Outcome, AuditOutcome.Failure, StringComparison.Ordinal)
            ? AuditSeverities.Warning
            : AuditSeverities.Info;

    /// <inheritdoc />
    public string? TenantId { get; init; }

    /// <inheritdoc />
    public string? TenantEnvironmentType { get; init; }

    /// <inheritdoc />
    public string? SubjectId { get; init; }

    /// <inheritdoc />
    public string? ClientId { get; init; }

    /// <inheritdoc />
    public AuditActor Actor { get; init; } = AuditActor.Anonymous;

    /// <inheritdoc />
    public AuditRequest? Request { get; init; }

    /// <inheritdoc />
    /// <remarks>
    /// Derived events override this to compute the resources from their typed properties, listing the
    /// target first and then the resources that own it.
    /// </remarks>
    public virtual IReadOnlyList<AuditResource> Resources => [];

    /// <inheritdoc />
    public AuditError? Error { get; init; }

    /// <inheritdoc />
    public required string Outcome { get; init; }
}
