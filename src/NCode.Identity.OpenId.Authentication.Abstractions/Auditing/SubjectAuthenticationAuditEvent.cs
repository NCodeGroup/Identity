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
