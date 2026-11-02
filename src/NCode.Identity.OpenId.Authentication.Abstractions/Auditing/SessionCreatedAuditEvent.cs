// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// An audit event raised when a server-side end-user session is established at sign-in.
/// </summary>
[PublicAPI]
public sealed record SessionCreatedAuditEvent : AuditEvent
{
    /// <inheritdoc />
    public override string EventName => OpenIdAuditEventNames.SessionCreated;

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Session;

    /// <summary>
    /// Gets the opaque session identifier (<c>sid</c>) that was created.
    /// </summary>
    public required string SessionId { get; init; }

    /// <inheritdoc />
    /// <remarks>
    /// Lists the created session as the event's only resource.
    /// </remarks>
    public override IReadOnlyList<AuditResource> Resources =>
        [new AuditResource(AuditResourceTypes.Session, SessionId)];
}
