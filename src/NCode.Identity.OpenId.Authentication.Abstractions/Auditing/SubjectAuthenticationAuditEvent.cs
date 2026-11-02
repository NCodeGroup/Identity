// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// An audit event raised when the server authenticates an end-user subject for a protocol flow. The
/// <see cref="IAuditEvent.Outcome"/> distinguishes a successful authentication from a failed one;
/// requests that present no credentials at all are not audited.
/// </summary>
[PublicAPI]
public sealed record SubjectAuthenticationAuditEvent : AuditEvent, ISupportAuditDetails
{
    /// <inheritdoc />
    public override string EventName => OpenIdAuditEventNames.SubjectAuthentication;

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Authentication;

    /// <summary>
    /// Gets the unverified user name a failed attempt presented, or <see langword="null"/> when none was presented.
    /// It is kept out of <see cref="IAuditEvent.SubjectId"/>, which holds only verified subject identifiers.
    /// </summary>
    public string? AttemptedUsername { get; init; }

    /// <inheritdoc />
    public bool HasAuditDetails => AttemptedUsername is not null;

    /// <inheritdoc />
    /// <remarks>
    /// Writes the <c>attempted_username</c> when one was presented; never a credential.
    /// </remarks>
    public void WriteAuditDetails(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (AttemptedUsername is not null)
            writer.WriteString("attempted_username", AttemptedUsername);
    }
}
