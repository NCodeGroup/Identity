// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// An audit event raised when authenticating a client at the token endpoint fails.
/// </summary>
[PublicAPI]
public sealed record ClientAuthenticationFailedAuditEvent
    : AuditEvent,
        ISupportMetricAttributes,
        ISupportAuditDetails
{
    /// <inheritdoc />
    public override string EventName => OpenIdAuditEventNames.ClientAuthentication;

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Authentication;

    /// <summary>
    /// The name of the metric attribute that carries <see cref="AuthenticationMethod"/> on the counter of published
    /// events.
    /// </summary>
    public const string AuthenticationMethodMetricAttribute =
        "ncode.identity.client_authentication.method";

    /// <summary>
    /// Gets the registered client-authentication method the client attempted, such as <c>private_key_jwt</c>.
    /// </summary>
    public required string AuthenticationMethod { get; init; }

    /// <inheritdoc />
    public void AddMetricAttributes(ref TagList tags) =>
        tags.Add(AuthenticationMethodMetricAttribute, AuthenticationMethod);

    /// <inheritdoc />
    /// <remarks>
    /// Writes the <c>authentication_method</c> the client attempted; no credential material is written.
    /// </remarks>
    public void WriteAuditDetails(Utf8JsonWriter writer) =>
        writer.WriteString("authentication_method", AuthenticationMethod);
}
