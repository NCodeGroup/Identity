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
    DateTimeOffset EventTime { get; }

    /// <summary>
    /// Gets the stable, dotted name that identifies the audited operation (for example
    /// <c>token.issued</c>).
    /// </summary>
    string EventName { get; }

    /// <summary>
    /// Gets the category that groups the audited operation (for example
    /// <see cref="AuditEventCategories.Token"/>). This is an extensible string vocabulary, not a
    /// closed set.
    /// </summary>
    string EventCategory { get; }

    /// <summary>
    /// Gets the service or component family that produced the audited event (for example
    /// <see cref="AuditEventSources.OpenId"/>). This is an extensible string vocabulary, not a closed
    /// set.
    /// </summary>
    string EventSource { get; }

    /// <summary>
    /// Gets the severity of the audited operation, such as <see cref="AuditSeverities.Info"/> or
    /// <see cref="AuditSeverities.Warning"/>. This is an extensible string vocabulary, not a closed
    /// set.
    /// </summary>
    string Severity { get; }

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
    /// Gets the identifier of the client involved in the audited operation, or <see langword="null"/>
    /// when there is no client.
    /// </summary>
    string? ClientId { get; }

    /// <summary>
    /// Gets the caller that performed the audited operation, such as an end user, a client or an
    /// administrator. This is <see cref="AuditActor.Anonymous"/> when the caller has not been
    /// identified.
    /// </summary>
    AuditActor Actor { get; }

    /// <summary>
    /// Gets the context of the request that the audited operation was part of (trace, network and
    /// endpoint details), captured at publish time, or <see langword="null"/> when the operation was
    /// not performed in the context of a request.
    /// </summary>
    AuditRequest? Request { get; }

    /// <summary>
    /// Gets the entities the audited operation acted on. The target comes first, followed by the
    /// resources that own it. This is empty when the operation acted on no distinct entity.
    /// </summary>
    IReadOnlyList<AuditResource> Resources { get; }

    /// <summary>
    /// Gets the machine-readable error that caused the audited operation to fail, or
    /// <see langword="null"/> when the operation did not fail.
    /// </summary>
    AuditError? Error { get; }

    /// <summary>
    /// Gets the outcome code of the audited operation, such as <see cref="AuditOutcome.Success"/> or
    /// <see cref="AuditOutcome.Failure"/>. This is an extensible string vocabulary, not a closed set.
    /// </summary>
    string Outcome { get; }
}
