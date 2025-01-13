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
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Subject;

/// <summary>
/// Contains the result after attempting to authenticate an end-user.
/// </summary>
[PublicAPI]
public readonly struct AuthenticateSubjectResult
{
    /// <summary>
    /// Gets the <see cref="IOpenIdError"/> for a failed authentication result.
    /// </summary>
    public IOpenIdError? Error { get; }

    /// <summary>
    /// Gets the <see cref="SubjectAuthentication"/> for a successful authentication result.
    /// </summary>
    public SubjectAuthentication? Ticket { get; }

    /// <summary>
    /// Gets a boolean indicating whether the result is undefined.
    /// </summary>
    public bool IsUndefined => Error == null && Ticket == null;

    /// <summary>
    /// Gets a boolean indicating whether authentication failed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsError => Error != null;

    /// <summary>
    /// Gets a boolean indicating whether authentication succeeded.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Ticket))]
    public bool IsSuccess => Ticket != null;

    /// <summary>
    /// Initializes a new instance of <see cref="AuthenticateSubjectResult"/> for when authentication failed.
    /// </summary>
    /// <param name="error">The <see cref="IOpenIdError"/> for a failed authentication result.</param>
    public AuthenticateSubjectResult(IOpenIdError error)
    {
        Error = error;
        Ticket = null;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AuthenticateSubjectResult"/> for when authentication succeeded.
    /// </summary>
    /// <param name="ticket">The <see cref="SubjectAuthentication"/> for a successful authentication result.</param>
    public AuthenticateSubjectResult(SubjectAuthentication ticket)
    {
        Error = null;
        Ticket = ticket;
    }
}
