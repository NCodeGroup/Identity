#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using NCode.Identity.OpenId.Endpoints.Authorization.Results;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Commands;

/// <summary>
/// Represents the disposition of authorizing a subject (aka end-user).
/// </summary>
/// <param name="RequiresChallenge">Indicates whether the authorization request requires a challenge from the user-agent.</param>
/// <param name="AuthorizationResult">Contains the optional <see cref="Results.AuthorizationResult"/> to be returned to the user-agent.</param>
[PublicAPI]
public readonly record struct AuthorizeSubjectDisposition(
    bool RequiresChallenge,
    AuthorizationResult? AuthorizationResult = null
)
{
    /// <summary>
    /// Gets a value indicating whether <see cref="AuthorizationResult"/> is not <see langword="null"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(AuthorizationResult))]
    public bool HasAuthorizationResult => AuthorizationResult is not null;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizeSubjectDisposition"/> class.
    /// </summary>
    public AuthorizeSubjectDisposition()
        : this(RequiresChallenge: false, AuthorizationResult: null)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizeSubjectDisposition"/> class
    /// that contains an <see cref="Results.AuthorizationResult"/> to be returned to the user-agent.
    /// </summary>
    /// <param name="authorizationResult">The <see cref="AuthorizationResult"/> to be returned to the user-agent.</param>
    public AuthorizeSubjectDisposition(AuthorizationResult authorizationResult)
        : this(RequiresChallenge: false, authorizationResult)
    {
        // nothing
    }
}
