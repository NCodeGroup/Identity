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

using JetBrains.Annotations;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Contains the inputs used to resolve the <c>acr</c> (Authentication Context Class Reference) value the authorization
/// server asserts for an authentication event.
/// </summary>
[PublicAPI]
public readonly record struct AuthenticationContextReferenceRequest
{
    /// <summary>
    /// Gets the authentication method references (the <c>amr</c> claim values) that were satisfied, such as
    /// <see cref="OpenIdConstants.AuthenticationMethods.Password"/>.
    /// </summary>
    public required IReadOnlyCollection<string> AuthenticationMethods { get; init; }

    /// <summary>
    /// Gets the identifier of the identity provider (the <c>idp</c> claim value) that authenticated the end-user, or
    /// <see langword="null"/> when the OP itself authenticated the end-user.
    /// </summary>
    public string? IdentityProvider { get; init; }

    /// <summary>
    /// Gets the effective settings governing the authentication, used to honor configured <c>acr</c> policy.
    /// </summary>
    public required IReadOnlySettingCollection Settings { get; init; }
}
