// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Passwords;

/// <summary>
/// Decides whether a whole password is on a list of commonly used, expected, or compromised passwords. The blocklists
/// are a strategy collection that a deployment extends by registering another, for example a breached-password
/// service.
/// </summary>
// See ADR-1236.
[PublicAPI]
public interface IPasswordBlocklist
{
    /// <summary>
    /// Determines whether the password is on the list.
    /// </summary>
    /// <param name="context">The <see cref="PasswordPolicyContext"/> of the account the password is set for.</param>
    /// <param name="password">The password to check, as a whole. The default policy passes its Unicode
    /// Normalization Form C (NFC).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    /// <returns><see langword="true"/> when the password is on the list; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> ContainsAsync(
        PasswordPolicyContext context,
        ReadOnlyMemory<char> password,
        CancellationToken cancellationToken
    );
}
