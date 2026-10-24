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

namespace NCode.Identity.OpenId.Authentication.Passwords;

/// <summary>
/// Contains the known values of <see cref="PasswordPolicyResult.Error"/>, the codes by which an
/// <see cref="IPasswordPolicy"/> names the reason it rejects a password. The vocabulary is open: a replacement policy
/// may report other codes.
/// </summary>
// See ADR-0236.
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
