// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Passwords;

/// <summary>
/// Judges a password that is being set for an account, whether created or reset by an administrator, changed by the
/// end user, or created by a seeder. The default policy requires the length the tenant's settings set and refuses a
/// password that any registered <see cref="IPasswordBlocklist"/> lists; it imposes no composition rules.
/// </summary>
// See ADR-1236.
[PublicAPI]
public interface IPasswordPolicy
{
    /// <summary>
    /// Validates the password that is being set for the account.
    /// </summary>
    /// <param name="context">The <see cref="PasswordPolicyContext"/> of the account the password is set for.</param>
    /// <param name="password">The password as submitted. The default policy judges its Unicode Normalization Form C
    /// (NFC), the form a store hashes.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    /// <returns>
    /// <see cref="PasswordPolicyResult.Accepted"/> when the password may be set; otherwise, the rejected
    /// <see cref="PasswordPolicyResult"/> that names the requirement the password does not meet.
    /// </returns>
    ValueTask<PasswordPolicyResult> ValidateAsync(
        PasswordPolicyContext context,
        ReadOnlyMemory<char> password,
        CancellationToken cancellationToken
    );
}
