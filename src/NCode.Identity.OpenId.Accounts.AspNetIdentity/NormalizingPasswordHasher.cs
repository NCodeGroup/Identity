// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Text;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Identity;

namespace NCode.Identity.OpenId.Accounts.AspNetIdentity;

/// <summary>
/// Decorates a host's ASP.NET Core Identity <see cref="IPasswordHasher{TUser}"/> so it hashes and verifies a
/// password's Unicode Normalization Form C (NFC). A hash made from the password as provided still verifies as a
/// fallback when the NFC form differs and does not verify, and the result asks the <see cref="UserManager{TUser}"/> to
/// replace it with a hash of the NFC form. A password with an unpaired surrogate has no NFC form and is used as
/// provided.
/// </summary>
/// <param name="innerHasher">The host's <see cref="IPasswordHasher{TUser}"/> that makes and verifies the hashes.</param>
/// <typeparam name="TUser">The ASP.NET Core Identity user type.</typeparam>
// See ADR-1238.
[PublicAPI]
public sealed class NormalizingPasswordHasher<TUser>(IPasswordHasher<TUser> innerHasher)
    : IPasswordHasher<TUser>
    where TUser : class
{
    private IPasswordHasher<TUser> InnerHasher { get; } =
        innerHasher ?? throw new ArgumentNullException(nameof(innerHasher));

    /// <inheritdoc />
    public string HashPassword(TUser user, string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var normalized = Normalize(password);
        return InnerHasher.HashPassword(user, normalized);
    }

    /// <inheritdoc />
    public PasswordVerificationResult VerifyHashedPassword(
        TUser user,
        string hashedPassword,
        string providedPassword
    )
    {
        ArgumentNullException.ThrowIfNull(providedPassword);

        var normalized = Normalize(providedPassword);
        var result = InnerHasher.VerifyHashedPassword(user, hashedPassword, normalized);
        if (
            result != PasswordVerificationResult.Failed
            || string.Equals(normalized, providedPassword, StringComparison.Ordinal)
        )
        {
            return result;
        }

        // A stored hash of the password as provided still verifies and is replaced by a hash of the NFC form.
        var providedResult = InnerHasher.VerifyHashedPassword(
            user,
            hashedPassword,
            providedPassword
        );
        return providedResult == PasswordVerificationResult.Failed
            ? PasswordVerificationResult.Failed
            : PasswordVerificationResult.SuccessRehashNeeded;
    }

    // A password with an unpaired surrogate has no NFC form, which string.Normalize refuses, so it is used as provided.
    private static string Normalize(string password) =>
        IsWellFormed(password) ? password.Normalize(NormalizationForm.FormC) : password;

    private static bool IsWellFormed(ReadOnlySpan<char> text)
    {
        while (!text.IsEmpty)
        {
            var status = Rune.DecodeFromUtf16(text, out _, out var consumed);
            if (status != OperationStatus.Done)
            {
                return false;
            }

            text = text[consumed..];
        }

        return true;
    }
}
