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

namespace NCode.Identity.OpenId.Authentication.Passwords;

/// <summary>
/// Refuses a password that is expected for the account: its own user name, its email address, or the local part of
/// that address, ignoring case. Only the whole password is compared.
/// </summary>
// See ADR-0236.
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
