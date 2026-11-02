// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Sessions;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// An audit event raised when a server-side end-user session (or every session a subject holds) is terminated.
/// </summary>
[PublicAPI]
public sealed record SessionTerminatedAuditEvent : AuditEvent, ISupportAuditDetails
{
    /// <inheritdoc />
    public override string EventName => OpenIdAuditEventNames.SessionTerminated;

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Session;

    /// <summary>
    /// Gets the opaque session identifier (<c>sid</c>) that was terminated, or <see langword="null"/> for a
    /// subject-wide termination.
    /// </summary>
    public required string? SessionId { get; init; }

    /// <summary>
    /// Gets the reason the session was terminated: one of <see cref="SessionTerminationReasons"/> or an extension's own
    /// name.
    /// </summary>
    public required string TerminationReason { get; init; }

    /// <inheritdoc />
    /// <remarks>
    /// Lists the terminated session as the event's only resource, or none for a subject-wide termination.
    /// </remarks>
    public override IReadOnlyList<AuditResource> Resources =>
        SessionId is null ? [] : [new AuditResource(AuditResourceTypes.Session, SessionId)];

    /// <inheritdoc />
    /// <remarks>
    /// Writes the <c>termination_reason</c>.
    /// </remarks>
    public void WriteAuditDetails(Utf8JsonWriter writer) =>
        writer.WriteString("termination_reason", TerminationReason);
}
