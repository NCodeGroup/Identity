// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Represents the disposition of authenticating a subject (the end-user).
/// </summary>
/// <param name="Error">Contains the <see cref="IOpenIdError"/> for a failed authentication result.</param>
/// <param name="Ticket">Contains the <see cref="SubjectAuthentication"/> for a successful authentication result.</param>
[PublicAPI]
public readonly record struct AuthenticateSubjectDisposition(
    IOpenIdError? Error,
    SubjectAuthentication? Ticket
)
{
    /// <summary>
    /// Gets a boolean indicating whether the result is undefined.
    /// </summary>
    public bool IsUndefined => Error == null && Ticket == null;

    /// <summary>
    /// Gets a boolean indicating whether authentication failed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool HasError => Error != null;

    /// <summary>
    /// Gets a boolean indicating whether authentication succeeded.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Ticket))]
    public bool IsAuthenticated => Ticket != null;

    /// <summary>
    /// Initializes a new instance of <see cref="AuthenticateSubjectDisposition"/> for when authentication failed.
    /// </summary>
    /// <param name="error">The <see cref="IOpenIdError"/> for a failed authentication result.</param>
    public AuthenticateSubjectDisposition(IOpenIdError error)
        // ReSharper disable once IntroduceOptionalParameters.Global
        : this(error, Ticket: null)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AuthenticateSubjectDisposition"/> for when authentication succeeded.
    /// </summary>
    /// <param name="ticket">The <see cref="SubjectAuthentication"/> for a successful authentication result.</param>
    public AuthenticateSubjectDisposition(SubjectAuthentication ticket)
        : this(Error: null, ticket)
    {
        // nothing
    }
}
