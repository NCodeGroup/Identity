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
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Accounts;

/// <summary>
/// Contains constants for the maximum lengths of local account fields.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public static class AccountMaxLengths
{
    /// <summary>
    /// Specifies the maximum length of an account's <c>UserName</c> (login handle).
    /// </summary>
    public const int UserName = 300;

    /// <summary>
    /// Specifies the maximum length of an account's <c>Email</c> address.
    /// </summary>
    public const int Email = 300;

    /// <summary>
    /// Specifies the maximum length of an account's hashed password.
    /// </summary>
    public const int PasswordHash = 1000;

    /// <summary>
    /// Specifies the maximum length of an account's <c>SecurityStamp</c>.
    /// </summary>
    public const int SecurityStamp = 100;

    /// <summary>
    /// Specifies the maximum length of a profile claim's <c>Type</c>.
    /// </summary>
    public const int ClaimType = 300;

    /// <summary>
    /// Specifies the maximum length of a profile claim's <c>Value</c>.
    /// </summary>
    public const int ClaimValue = 2000;
}
