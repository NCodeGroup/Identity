// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
