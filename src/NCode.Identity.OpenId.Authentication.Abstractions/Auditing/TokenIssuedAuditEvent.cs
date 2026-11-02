// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// An audit event raised when a security token is issued. It carries the token's non-sensitive
/// metadata only; the token value itself is never present on the event.
/// </summary>
[PublicAPI]
public sealed record TokenIssuedAuditEvent
    : AuditEvent,
        ISupportMetricAttributes,
        ISupportAuditDetails
{
    /// <inheritdoc />
    public override string EventName => OpenIdAuditEventNames.TokenIssued;

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Token;

    /// <summary>
    /// Gets the type of the issued token (for example an access token, ID token, refresh token, or
    /// authorization code).
    /// </summary>
    public required string TokenType { get; init; }

    /// <summary>
    /// The name of the metric attribute that carries <see cref="TokenType"/> on the counter of published events.
    /// </summary>
    public const string TokenTypeMetricAttribute = "ncode.identity.token.type";

    /// <inheritdoc />
    public void AddMetricAttributes(ref TagList tags) =>
        tags.Add(TokenTypeMetricAttribute, TokenType);

    /// <inheritdoc />
    /// <remarks>
    /// Writes the <c>token_type</c>; the token value itself is never written.
    /// </remarks>
    public void WriteAuditDetails(Utf8JsonWriter writer) =>
        writer.WriteString("token_type", TokenType);
}
