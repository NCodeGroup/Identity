// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.Bff;

/// <summary>
/// An audit event raised by the <c>Backend for Frontend (BFF)</c>. The event seam counts it on
/// <c>ncode.identity.events.published</c>, with its reason as <c>ncode.identity.bff.reason</c>, so logins, session ends,
/// refresh failures and rejections are counted once, by their reason.
/// </summary>
[PublicAPI]
public sealed record BffAuditEvent : AuditEvent, ISupportAuditDetails, ISupportMetricAttributes
{
    /// <summary>
    /// The name of the metric attribute that carries the event's reason, a value from the BFF's reason vocabularies,
    /// such as <see cref="BffLoginFailureReasons"/> or <see cref="BffSessionEndReasons"/>.
    /// </summary>
    // See ADR-1197.
    public const string ReasonMetricAttribute = "ncode.identity.bff.reason";

    /// <summary>
    /// Gets the name of the event, such as one of the values of <see cref="BffAuditEventNames"/>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the category of the event.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Gets the reason for the event, such as a login failure reason or a session end reason.
    /// </summary>
    public string? Reason { get; init; }

    /// <inheritdoc />
    public override string EventName => Name;

    /// <inheritdoc />
    public override string EventCategory => Category;

    /// <inheritdoc />
    public bool HasAuditDetails => Reason is not null;

    /// <inheritdoc />
    /// <remarks>
    /// Writes the <c>reason</c> when there is one; no token, handle or cookie value is ever written.
    /// </remarks>
    public void WriteAuditDetails(Utf8JsonWriter writer)
    {
        if (Reason is not null)
            writer.WriteString("reason", Reason);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Adds the <c>reason</c> when there is one: a BFF constant, never a value a caller supplies.
    /// </remarks>
    public void AddMetricAttributes(ref TagList tags)
    {
        if (Reason is not null)
            tags.Add(ReasonMetricAttribute, Reason);
    }
}
