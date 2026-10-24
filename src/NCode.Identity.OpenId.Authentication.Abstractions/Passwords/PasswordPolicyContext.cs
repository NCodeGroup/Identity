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

namespace NCode.Identity.OpenId.Authentication.Passwords;

/// <summary>
/// Carries what an <see cref="IPasswordPolicy"/> and an <see cref="IPasswordBlocklist"/> need to know about the account
/// a password is being set for: the settings of its tenant and the names it is known by.
/// </summary>
// See ADR-0236.
[PublicAPI]
public sealed class PasswordPolicyContext
{
    /// <summary>
    /// Gets the effective settings of the tenant the account belongs to.
    /// </summary>
    public required IReadOnlySettingCollection Settings { get; init; }

    /// <summary>
    /// Gets the user name of the account.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Gets the email address of the account, or <see langword="null"/> when the account has none.
    /// </summary>
    public string? Email { get; init; }
}
