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
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Passwords;

/// <summary>
/// Limits the failed password attempts of an account, so a password can be guessed only a limited number of times.
/// Every path that verifies a password passes <see cref="IsLimitReachedAsync"/> to the store's verification, which
/// refuses a password of an account at its limit by verifying it against the dummy hash, and answers it as it answers
/// a wrong password.
/// </summary>
// See ADR-0237.
[PublicAPI]
public interface IPasswordAttemptLimiter
{
    /// <summary>
    /// Determines whether the account that the user name names has reached the tenant's limit of failed attempts, so a
    /// password presented for it is refused and verified against the store's dummy hash instead of the account's hash.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="userName">The user name that the password is presented for.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    /// <returns>
    /// The <see cref="ValueTask{TResult}"/> that represents the asynchronous operation, containing <c>true</c> when the
    /// account has reached the limit; otherwise, <c>false</c>.
    /// </returns>
    ValueTask<bool> IsLimitReachedAsync(
        OpenIdContext openIdContext,
        string userName,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records one failed password attempt, a refusal at the limit included, for the account that the user name names.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="userName">The user name that the failed password was presented for.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask RecordFailureAsync(
        OpenIdContext openIdContext,
        string userName,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Clears the failed attempts of the account that the user name names, after a verified password or after a
    /// password that an administrator sets.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant that the account belongs to.</param>
    /// <param name="userName">The user name of the account.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask ResetAsync(string tenantId, string userName, CancellationToken cancellationToken);
}
