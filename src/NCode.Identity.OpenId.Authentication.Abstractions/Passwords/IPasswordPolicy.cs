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
/// Judges a password that is being set for an account, whether created or reset by an administrator, changed by the
/// end user, or created by a seeder. The default policy requires the length the tenant's settings set and refuses a
/// password that any registered <see cref="IPasswordBlocklist"/> lists; it imposes no composition rules.
/// </summary>
// See ADR-0236.
[PublicAPI]
public interface IPasswordPolicy
{
    /// <summary>
    /// Validates the password that is being set for the account.
    /// </summary>
    /// <param name="context">The <see cref="PasswordPolicyContext"/> of the account the password is set for.</param>
    /// <param name="password">The password to validate.</param>
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
