// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// An audit event raised when an administrator revokes a single grant.
/// </summary>
[PublicAPI]
public sealed record GrantRevokedAuditEvent : AuditEvent, ISupportAuditDetails
{
    /// <inheritdoc />
    public override string EventName => ManagementAuditEventNames.GrantRevoked;

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Management;

    /// <summary>
    /// Gets the type of the revoked grant, if known.
    /// </summary>
    public string? GrantType { get; init; }

    /// <inheritdoc />
    /// <remarks>
    /// Writes the <c>grant_type</c> when it is known.
    /// </remarks>
    public void WriteAuditDetails(Utf8JsonWriter writer)
    {
        if (GrantType is not null)
            writer.WriteString("grant_type", GrantType);
    }
}
