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

using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Accounts.Credentials;

/// <summary>
/// Provides the default implementation of <see cref="IPasswordHasher"/> using PBKDF2-HMAC-SHA256 over the modern,
/// span-based <see cref="Rfc2898DeriveBytes.Pbkdf2(ReadOnlySpan{byte}, ReadOnlySpan{byte}, Span{byte}, int, HashAlgorithmName)"/>
/// primitive. The produced hash is a self-describing string carrying the scheme, iteration count, salt, and derived key
/// so that iteration counts can be upgraded over time (rehash-on-verify).
/// </summary>
internal sealed class DefaultPasswordHasher(IOptions<PasswordHasherOptions> optionsAccessor)
    : IPasswordHasher
{
    private const string SchemeId = "ncode-pbkdf2-sha256";
    private const int MaxByteCount = 256;

    private static HashAlgorithmName Algorithm => HashAlgorithmName.SHA256;

    private PasswordHasherOptions Options { get; } = Validate(optionsAccessor.Value);

    private static PasswordHasherOptions Validate(PasswordHasherOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Iterations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.SaltByteCount, 8);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.SaltByteCount, MaxByteCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.HashByteCount, 16);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.HashByteCount, MaxByteCount);
        return options;
    }

    /// <inheritdoc />
    public string HashPassword(ReadOnlySpan<byte> password)
    {
        var iterations = Options.Iterations;

        Span<byte> salt = stackalloc byte[Options.SaltByteCount];
        Span<byte> hash = stackalloc byte[Options.HashByteCount];
        try
        {
            RandomNumberGenerator.Fill(salt);
            Rfc2898DeriveBytes.Pbkdf2(password, salt, hash, iterations, Algorithm);

            return string.Concat(
                "$",
                SchemeId,
                "$",
                iterations.ToString(CultureInfo.InvariantCulture),
                "$",
                Base64Url.EncodeToString(salt),
                "$",
                Base64Url.EncodeToString(hash)
            );
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hash);
        }
    }

    /// <inheritdoc />
    public PasswordVerificationResult VerifyHashedPassword(
        string hashedPassword,
        ReadOnlySpan<byte> password
    )
    {
        // $ncode-pbkdf2-sha256$<iterations>$<saltBase64Url>$<hashBase64Url>
        var segments = hashedPassword.Split('$');
        if (
            segments.Length != 5
            || segments[0].Length != 0
            || !string.Equals(segments[1], SchemeId, StringComparison.Ordinal)
            || !int.TryParse(
                segments[2],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var iterations
            )
            || iterations < 1
        )
        {
            return PasswordVerificationResult.Failed;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Base64Url.DecodeFromChars(segments[3]);
            expectedHash = Base64Url.DecodeFromChars(segments[4]);
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }

        if (expectedHash.Length is < 16 or > MaxByteCount)
        {
            return PasswordVerificationResult.Failed;
        }

        var matched = false;
        Span<byte> computed = stackalloc byte[expectedHash.Length];
        try
        {
            Rfc2898DeriveBytes.Pbkdf2(password, salt, computed, iterations, Algorithm);
            matched = CryptographicOperations.FixedTimeEquals(computed, expectedHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(computed);
        }

        if (!matched)
        {
            return PasswordVerificationResult.Failed;
        }

        return iterations < Options.Iterations
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Success;
    }
}
