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
/// Decides whether a whole password is on a list of commonly used, expected, or compromised passwords. The blocklists
/// are a strategy collection that a deployment extends by registering another, for example a breached-password
/// service.
/// </summary>
// See ADR-0236.
[PublicAPI]
public interface IPasswordBlocklist
{
    /// <summary>
    /// Determines whether the password is on the list.
    /// </summary>
    /// <param name="context">The <see cref="PasswordPolicyContext"/> of the account the password is set for.</param>
    /// <param name="password">The password to check, as a whole.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    /// <returns><see langword="true"/> when the password is on the list; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> ContainsAsync(
        PasswordPolicyContext context,
        ReadOnlyMemory<char> password,
        CancellationToken cancellationToken
    );
}
