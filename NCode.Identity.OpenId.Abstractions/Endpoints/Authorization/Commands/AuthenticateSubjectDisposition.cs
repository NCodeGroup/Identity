#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Subject;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Commands;

/// <summary>
/// Represents the disposition of authenticating a subject (aka end-user).
/// </summary>
/// <param name="Error">Contains the <see cref="IOpenIdError"/> for a failed authentication result.</param>
/// <param name="Ticket">Contains the <see cref="SubjectAuthentication"/> for a successful authentication result.</param>
[PublicAPI]
public readonly record struct AuthenticateSubjectDisposition(IOpenIdError? Error, SubjectAuthentication? Ticket)
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
