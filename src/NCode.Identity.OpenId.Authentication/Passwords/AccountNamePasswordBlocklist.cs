// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Passwords;

/// <summary>
/// Refuses a password that is expected for the account: its own user name, its email address, or the local part of
/// that address, ignoring case. Only the whole password is compared.
/// </summary>
// See ADR-1236.
internal class AccountNamePasswordBlocklist : IPasswordBlocklist
{
    /// <inheritdoc />
    public ValueTask<bool> ContainsAsync(
        PasswordPolicyContext context,
        ReadOnlyMemory<char> password,
        CancellationToken cancellationToken
    )
    {
        var span = password.Span;
        var listed = IsName(span, context.UserName);
        if (!listed && context.Email is { Length: > 0 } email)
        {
            var localPart = LocalPart(email);
            listed = IsName(span, email) || IsName(span, localPart);
        }

        return ValueTask.FromResult(listed);
    }

    private static bool IsName(ReadOnlySpan<char> password, ReadOnlySpan<char> name) =>
        name.Length > 0 && password.Equals(name, StringComparison.OrdinalIgnoreCase);

    private static ReadOnlySpan<char> LocalPart(string email)
    {
        var at = email.LastIndexOf('@');
        return at > 0 ? email.AsSpan(0, at) : ReadOnlySpan<char>.Empty;
    }
}
