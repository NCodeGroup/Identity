// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Accounts.Authentication;

/// <summary>
/// Asks the limit on failed password attempts on behalf of a local-account store's verification and remembers the
/// answer, so a store that does not ask it, or verifies a password it refused, cannot lift the limit.
/// </summary>
// See ADR-1237.
internal sealed class PasswordAttemptGate(
    IPasswordAttemptLimiter passwordAttemptLimiter,
    OpenIdContext openIdContext
)
{
    private IPasswordAttemptLimiter PasswordAttemptLimiter { get; } = passwordAttemptLimiter;
    private OpenIdContext OpenIdContext { get; } = openIdContext;
    private bool IsAsked { get; set; }
    private bool IsRefused { get; set; }

    /// <summary>
    /// Asks whether the account that holds the specified user name reached its limit of failed password attempts. A
    /// caller passes this function as the <c>isRefusedAsync</c> argument of the store's verification.
    /// </summary>
    /// <param name="userName">The user name that the store holds for the account, or the submitted one when no account
    /// has it.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> when the account reached its limit; otherwise, <c>false</c>.</returns>
    public async ValueTask<bool> IsRefusedAsync(
        string userName,
        CancellationToken cancellationToken
    )
    {
        IsAsked = true;
        IsRefused = await PasswordAttemptLimiter.IsLimitReachedAsync(
            OpenIdContext,
            userName,
            cancellationToken
        );
        return IsRefused;
    }

    /// <summary>
    /// Returns the answer that the limit allows for the specified result of the store's verification. A result that
    /// the store verified after the gate refused is answered as a refusal that carries no account.
    /// </summary>
    /// <param name="result">The result of the store's verification.</param>
    /// <returns>The specified result, or a refusal when the gate refused and the result is not one.</returns>
    /// <exception cref="InvalidOperationException">The store verified the password without asking the gate.</exception>
    public LocalAccountVerificationResult Enforce(LocalAccountVerificationResult result)
    {
        if (!IsAsked)
        {
            throw new InvalidOperationException(
                "The local-account store verified a password without calling isRefusedAsync, so the limit on failed "
                    + "password attempts would not apply. A store calls it once, between its lookup and its "
                    + "verification."
            );
        }

        if (IsRefused && (result.IsVerified || !result.IsRefused))
        {
            return new LocalAccountVerificationResult
            {
                UserName = result.UserName,
                IsRefused = true,
            };
        }

        return result;
    }
}
