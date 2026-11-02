// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// An audit event raised when an administrator bulk-revokes grants by subject, client, and/or session filter.
/// </summary>
[PublicAPI]
public sealed record GrantsRevokedBulkAuditEvent : AuditEvent, ISupportAuditDetails
{
    /// <inheritdoc />
    public override string EventName => ManagementAuditEventNames.GrantsRevokedBulk;

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Management;

    /// <summary>
    /// Gets the opaque <c>sid</c> filter applied to the revocation, if any.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Gets the number of grants that were revoked.
    /// </summary>
    public long RevokedCount { get; init; }

    /// <inheritdoc />
    /// <remarks>
    /// Lists the session the revocation was filtered to as the event's only resource, or none when it was not filtered
    /// by session.
    /// </remarks>
    public override IReadOnlyList<AuditResource> Resources =>
        SessionId is null ? [] : [new AuditResource(AuditResourceTypes.Session, SessionId)];

    /// <inheritdoc />
    /// <remarks>
    /// Writes the <c>revoked_count</c>.
    /// </remarks>
    public void WriteAuditDetails(Utf8JsonWriter writer) =>
        writer.WriteNumber("revoked_count", RevokedCount);
}
