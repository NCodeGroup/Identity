// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Passwords;

/// <summary>
/// Carries what an <see cref="IPasswordPolicy"/> and an <see cref="IPasswordBlocklist"/> need to know about the account
/// a password is being set for: the settings of its tenant and the names it is known by.
/// </summary>
// See ADR-1236.
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
