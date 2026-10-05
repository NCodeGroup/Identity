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
/// An <see cref="IEvent"/> that carries the security-grade envelope every audit record needs, so a
/// single audit sink registered for this contract observes every audit event.
/// </summary>
/// <remarks>
/// An audit event carries identifiers and metadata, never secret material: no raw tokens, client
/// secrets, key material, or authorization codes ever appear on an audit event. Use
/// <see cref="AuditEvent"/> as the base for concrete audit events.
/// </remarks>
[PublicAPI]
public interface IAuditEvent : IEvent
{
    /// <summary>
    /// Gets the unique identifier of this audit event, distinct from the correlation identifier, so
    /// at-least-once delivery can be de-duplicated.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Gets the coordinated universal time (UTC) at which the audited operation occurred.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the version of the audit event's serialized schema, so external consumers can evolve
    /// independently of in-process API tracking.
    /// </summary>
    int SchemaVersion { get; }

    /// <summary>
    /// Gets the correlation or trace identifier that links this audit event to the originating
    /// activity, or <see langword="null"/> when none is available.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Gets the identifier of the tenant the audited operation belongs to, or <see langword="null"/>
    /// when not tenant-scoped.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Gets the identifier of the subject (actor) the audited operation acted on behalf of, or
    /// <see langword="null"/> when there is no subject.
    /// </summary>
    string? SubjectId { get; }

    /// <summary>
    /// Gets the identifier of the principal that performed the audited operation (for example an
    /// administrator for a management action), or <see langword="null"/> when there is no distinct
    /// actor. In subject-driven flows the actor is the subject and this may be left unset.
    /// </summary>
    string? ActorId { get; }

    /// <summary>
    /// Gets the identifier of the client involved in the audited operation, or <see langword="null"/>
    /// when there is no client.
    /// </summary>
    string? ClientId { get; }

    /// <summary>
    /// Gets the stable, dotted action name that identifies the audited operation (for example
    /// <c>token.issued</c>).
    /// </summary>
    string Action { get; }

    /// <summary>
    /// Gets an optional name of the endpoint, operation, or component that produced the audited
    /// event (for example <c>api/token</c>), or <see langword="null"/> when not applicable.
    /// </summary>
    string? Source { get; }

    /// <summary>
    /// Gets the outcome code of the audited operation, such as <see cref="AuditOutcome.Success"/> or
    /// <see cref="AuditOutcome.Failure"/>. This is an extensible string vocabulary, not a closed set.
    /// </summary>
    string Outcome { get; }

    /// <summary>
    /// Gets an optional human-readable reason that explains the outcome, typically populated on
    /// failure, or <see langword="null"/> when none applies.
    /// </summary>
    string? Reason { get; }
}
