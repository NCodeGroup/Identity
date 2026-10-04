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

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Tokens.Commands;

/// <summary>
/// Represents the disposition of authenticating a self-issued JWT access token.
/// </summary>
/// <param name="Error">Contains the <see cref="IOpenIdError"/> for a failed authentication result.</param>
/// <param name="Principal">Contains the authenticated <see cref="ClaimsPrincipal"/> for a successful result.</param>
[PublicAPI]
public readonly record struct AuthenticateAccessTokenDisposition(
    IOpenIdError? Error,
    ClaimsPrincipal? Principal
)
{
    /// <summary>
    /// Gets a boolean indicating whether the result is undefined (no token was presented).
    /// </summary>
    public bool IsUndefined => Error == null && Principal == null;

    /// <summary>
    /// Gets a boolean indicating whether authentication failed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool HasError => Error != null;

    /// <summary>
    /// Gets a boolean indicating whether authentication succeeded.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Principal))]
    public bool IsAuthenticated => Principal != null;

    /// <summary>
    /// Initializes a new instance of <see cref="AuthenticateAccessTokenDisposition"/> for when authentication failed.
    /// </summary>
    /// <param name="error">The <see cref="IOpenIdError"/> for a failed authentication result.</param>
    public AuthenticateAccessTokenDisposition(IOpenIdError error)
        // ReSharper disable once IntroduceOptionalParameters.Global
        : this(error, Principal: null)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AuthenticateAccessTokenDisposition"/> for when authentication succeeded.
    /// </summary>
    /// <param name="principal">The authenticated <see cref="ClaimsPrincipal"/> for a successful result.</param>
    public AuthenticateAccessTokenDisposition(ClaimsPrincipal principal)
        : this(Error: null, principal)
    {
        // nothing
    }
}
