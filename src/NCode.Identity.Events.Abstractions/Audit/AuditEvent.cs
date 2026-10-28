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
/// This type is a deliberate extension point; derive a concrete audit event from it and override
/// <see cref="Action"/> (and <see cref="SchemaVersion"/> when the serialized shape changes). An audit
/// event carries identifiers and metadata only, never secret material.
/// </remarks>
[PublicAPI]
public abstract record AuditEvent : IAuditEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; }

    /// <inheritdoc />
    public DateTimeOffset EventTime { get; init; }

    // Bridge: aliases EventTime until Timestamp is removed (plan step 33).
    /// <inheritdoc />
    public DateTimeOffset Timestamp
    {
        get => EventTime;
        init => EventTime = value;
    }

    // Bridge: reads Action until EventName becomes abstract (plan step 33). A derived event must
    // override EventName or Action, never neither.
    /// <inheritdoc />
    public virtual string EventName => Action;

    // Bridge: defaults to empty until EventCategory becomes abstract (plan step 33).
    /// <inheritdoc />
    public virtual string EventCategory => string.Empty;

    // Bridge: defaults to empty until EventSource becomes required (plan step 33).
    /// <inheritdoc />
    public string EventSource { get; init; } = string.Empty;

    /// <inheritdoc />
    public virtual string Severity =>
        string.Equals(Outcome, AuditOutcome.Failure, StringComparison.Ordinal)
            ? AuditSeverities.Warning
            : AuditSeverities.Info;

    /// <inheritdoc />
    public virtual int SchemaVersion => 1;

    /// <inheritdoc />
    public string? CorrelationId { get; init; }

    /// <inheritdoc />
    public string? TenantId { get; init; }

    /// <inheritdoc />
    public string? SubjectId { get; init; }

    /// <inheritdoc />
    public string? ActorId { get; init; }

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

    // Bridge: reads EventName until Action is removed (plan step 33).
    /// <inheritdoc />
    public virtual string Action => EventName;

    /// <inheritdoc />
    public string? Source { get; init; }

    /// <inheritdoc />
    public required string Outcome { get; init; }

    /// <inheritdoc />
    public string? Reason { get; init; }
}
