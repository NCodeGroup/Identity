// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Passwords;

/// <summary>
/// Contains the known values of <see cref="PasswordPolicyResult.Error"/>, the codes by which an
/// <see cref="IPasswordPolicy"/> names the reason it rejects a password. The vocabulary is open: a replacement policy
/// may report other codes.
/// </summary>
// See ADR-1236.
[PublicAPI]
public static class PasswordPolicyErrors
{
    /// <summary>
    /// Contains the code for a password that has fewer characters than the tenant requires.
    /// </summary>
    public const string TooShort = "password_too_short";

    /// <summary>
    /// Contains the code for a password that has more characters than the tenant allows.
    /// </summary>
    public const string TooLong = "password_too_long";

    /// <summary>
    /// Contains the code for a password that an <see cref="IPasswordBlocklist"/> lists as commonly used, expected, or
    /// compromised.
    /// </summary>
    public const string Blocklisted = "password_blocklisted";
}
