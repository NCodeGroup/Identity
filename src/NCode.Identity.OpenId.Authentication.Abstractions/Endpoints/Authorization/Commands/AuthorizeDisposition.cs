// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;

/// <summary>
/// Represents the disposition of authorizing a subject (the end-user).
/// </summary>
/// <param name="ChallengeRequired">Indicates whether the authorization request requires the user-agent to
/// (re-)authenticate.</param>
/// <param name="ConsentRequired">Indicates whether the authorization request requires the user-agent to grant consent
/// for the requested scopes (the subject is authenticated but has not consented).</param>
/// <param name="Error">Contains the <see cref="IOpenIdError"/> to be returned to the user-agent.</param>
[PublicAPI]
public readonly record struct AuthorizeDisposition(
    bool ChallengeRequired,
    bool ConsentRequired = false,
    IOpenIdError? Error = null
)
{
    /// <summary>
    /// Gets a value indicating whether <see cref="Error"/> is not <see langword="null"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool HasError => Error is not null;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizeDisposition"/> class.
    /// </summary>
    public AuthorizeDisposition()
        : this(ChallengeRequired: false, Error: null)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizeDisposition"/> class
    /// that contains an <see cref="IOpenIdError"/> to be returned to the user-agent.
    /// </summary>
    /// <param name="error">The <see cref="IOpenIdError"/> to be returned to the user-agent.</param>
    public AuthorizeDisposition(IOpenIdError error)
        : this(ChallengeRequired: false, Error: error)
    {
        // nothing
    }
}
