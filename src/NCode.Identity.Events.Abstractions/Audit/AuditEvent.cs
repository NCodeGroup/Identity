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
    public DateTimeOffset Timestamp { get; init; }

    /// <inheritdoc />
    public virtual int SchemaVersion => 1;

    /// <inheritdoc />
    public string? CorrelationId { get; init; }

    /// <inheritdoc />
    public string? TenantId { get; init; }

    /// <inheritdoc />
    public string? SubjectId { get; init; }

    /// <inheritdoc />
    public string? ClientId { get; init; }

    /// <inheritdoc />
    public abstract string Action { get; }

    /// <inheritdoc />
    public required string Outcome { get; init; }

    /// <inheritdoc />
    public string? Reason { get; init; }
}
