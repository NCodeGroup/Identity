// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// An audit event raised when the UserInfo endpoint answers a request whose access token authenticated: the claims it
/// disclosed about the subject to the client, or its refusal to disclose them. It carries the names of the claims,
/// never their values, and never the access token.
/// </summary>
[PublicAPI]
public sealed record UserInfoAccessedAuditEvent : AuditEvent, ISupportAuditDetails
{
    /// <inheritdoc />
    public override string EventName => OpenIdAuditEventNames.UserInfoAccessed;

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.DataAccess;

    /// <summary>
    /// Gets the names of the claims the response disclosed, in ordinal order; empty when the request was refused.
    /// </summary>
    public required IReadOnlyList<string> ClaimNames { get; init; }

    /// <inheritdoc />
    public bool HasAuditDetails => ClaimNames.Count > 0;

    /// <inheritdoc />
    /// <remarks>
    /// Writes the <c>claim_names</c> the response disclosed; no claim value is written.
    /// </remarks>
    public void WriteAuditDetails(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartArray("claim_names");
        foreach (var claimName in ClaimNames)
            writer.WriteStringValue(claimName);
        writer.WriteEndArray();
    }
}
