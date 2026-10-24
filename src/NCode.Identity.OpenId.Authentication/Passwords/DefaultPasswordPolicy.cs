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

using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using NCode.Buffers;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Authentication.Passwords;

/// <summary>
/// Judges the Unicode Normalization Form C (NFC) of a password, the form a store hashes, by the minimum and maximum
/// length the tenant's settings set, counted in Unicode code points, and then by every registered
/// <see cref="IPasswordBlocklist"/> in order. It imposes no composition rules and does not truncate the password.
/// </summary>
// See ADR-0236.
internal class DefaultPasswordPolicy(IEnumerable<IPasswordBlocklist> blocklists) : IPasswordPolicy
{
    private ImmutableArray<IPasswordBlocklist> Blocklists { get; } = [.. blocklists];

    /// <inheritdoc />
    public async ValueTask<PasswordPolicyResult> ValidateAsync(
        PasswordPolicyContext context,
        ReadOnlyMemory<char> password,
        CancellationToken cancellationToken
    )
    {
        var minLength = context.Settings.GetValue(OpenIdSettingKeys.PasswordMinLength);
        var maxLength = context.Settings.GetValue(OpenIdSettingKeys.PasswordMaxLength);

        // The policy judges the NFC form, the form a store hashes. See ADR-0238.
        using var lease = RentNormalized(password, out var normalized);

        var length = CountCodePoints(normalized.Span);

        if (length < minLength)
        {
            return PasswordPolicyResult.Rejected(
                PasswordPolicyErrors.TooShort,
                $"The password must have at least {minLength} characters."
            );
        }

        if (length > maxLength)
        {
            return PasswordPolicyResult.Rejected(
                PasswordPolicyErrors.TooLong,
                $"The password must have at most {maxLength} characters."
            );
        }

        foreach (var blocklist in Blocklists)
        {
            var listed = await blocklist.ContainsAsync(context, normalized, cancellationToken);
            if (listed)
            {
                return PasswordPolicyResult.Rejected(
                    PasswordPolicyErrors.Blocklisted,
                    "The password is commonly used, expected, or known to be compromised; choose another one."
                );
            }
        }

        return PasswordPolicyResult.Accepted;
    }

    // Returns the lease of a sensitive buffer holding the NFC form, or null when the password is already in NFC, in
    // which case normalized is the password itself.
    private static IMemoryOwner<char>? RentNormalized(
        ReadOnlyMemory<char> password,
        out ReadOnlyMemory<char> normalized
    )
    {
        normalized = password;
        var source = password.Span;
        if (source.IsNormalized(NormalizationForm.FormC))
        {
            return null;
        }

        var capacity = source.GetNormalizedLength(NormalizationForm.FormC);
        while (true)
        {
            var lease = BufferFactory.Rent<char>(
                capacity,
                isSensitive: true,
                out Memory<char> buffer
            );

            if (source.TryNormalize(buffer.Span, out var written, NormalizationForm.FormC))
            {
                normalized = buffer[..written];
                return lease;
            }

            lease.Dispose();
            capacity *= 2;
        }
    }

    // NIST SP 800-63B counts each Unicode code point as one character, so a surrogate pair is a single character.
    private static int CountCodePoints(ReadOnlySpan<char> password)
    {
        var count = 0;
        foreach (var _ in password.EnumerateRunes())
            count++;

        return count;
    }
}
