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
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Unicode;
using Microsoft.Extensions.Options;
using NCode.Buffers;

namespace NCode.Identity.OpenId.Accounts.Credentials;

/// <summary>
/// Provides the default implementation of <see cref="IPasswordHasher"/> using PBKDF2-HMAC-SHA256 over the modern,
/// span-based <see cref="Rfc2898DeriveBytes.Pbkdf2(ReadOnlySpan{byte}, ReadOnlySpan{byte}, Span{byte}, int, HashAlgorithmName)"/>
/// primitive. The produced hash is a self-describing string carrying the scheme, iteration count, salt, and derived key
/// so that iteration counts can be upgraded over time (rehash-on-verify). It hashes and verifies the Unicode
/// Normalization Form C (NFC) of the password, and verifies a stored hash of the password as submitted as a fallback
/// that requests a rehash.
/// </summary>
// See ADR-0238.
internal sealed class DefaultPasswordHasher : IPasswordHasher
{
    private const string SchemeId = "ncode-pbkdf2-sha256";
    private const int MaxByteCount = 256;

    private static HashAlgorithmName Algorithm => HashAlgorithmName.SHA256;

    public DefaultPasswordHasher(IOptions<PasswordHasherOptions> optionsAccessor)
    {
        Options = Validate(optionsAccessor.Value);
        DummyPasswordHash = CreateDummyPasswordHash(Options);
    }

    private PasswordHasherOptions Options { get; }

    /// <inheritdoc />
    public string DummyPasswordHash { get; }

    private static PasswordHasherOptions Validate(PasswordHasherOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Iterations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.SaltByteCount, 8);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.SaltByteCount, MaxByteCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.HashByteCount, 16);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.HashByteCount, MaxByteCount);
        return options;
    }

    private static string CreateDummyPasswordHash(PasswordHasherOptions options)
    {
        // A random salt and key at the current iterations: verifying against it costs what verifying a stored hash
        // of the current parameters costs, and no known password matches it. See ADR-0239.
        Span<byte> salt = stackalloc byte[options.SaltByteCount];
        Span<byte> key = stackalloc byte[options.HashByteCount];
        RandomNumberGenerator.Fill(salt);
        RandomNumberGenerator.Fill(key);
        return Format(options.Iterations, salt, key);
    }

    private static string Format(int iterations, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> hash)
    {
        var iterationsText = iterations.ToString(CultureInfo.InvariantCulture);
        var saltText = Base64Url.EncodeToString(salt);
        var hashText = Base64Url.EncodeToString(hash);
        return string.Concat("$", SchemeId, "$", iterationsText, "$", saltText, "$", hashText);
    }

    /// <inheritdoc />
    public string HashPassword(ReadOnlySpan<byte> password)
    {
        using var lease = RentNormalized(password, out var normalized);
        ReadOnlySpan<byte> bytes = lease is null ? password : normalized.Span;
        return HashCore(bytes);
    }

    /// <inheritdoc />
    public PasswordVerificationResult VerifyHashedPassword(
        string hashedPassword,
        ReadOnlySpan<byte> password
    )
    {
        using var lease = RentNormalized(password, out var normalized);
        if (lease is null)
        {
            return VerifyCore(hashedPassword, password);
        }

        var result = VerifyCore(hashedPassword, normalized.Span);
        if (result != PasswordVerificationResult.Failed)
        {
            return result;
        }

        // A stored hash of the password as submitted still verifies and is reported for a rehash, so the store
        // replaces it with a hash of the NFC form. See ADR-0238.
        var submittedResult = VerifyCore(hashedPassword, password);
        return submittedResult == PasswordVerificationResult.Failed
            ? PasswordVerificationResult.Failed
            : PasswordVerificationResult.SuccessRehashNeeded;
    }

    // Returns the lease of a sensitive buffer holding the UTF-8 bytes of the NFC form, or null when the password is
    // empty, is not valid UTF-8, or is already in NFC, in which case the password is used as it is.
    private static IMemoryOwner<byte>? RentNormalized(
        ReadOnlySpan<byte> password,
        out ReadOnlyMemory<byte> normalized
    )
    {
        normalized = default;
        if (password.IsEmpty)
        {
            return null;
        }

        using var charsLease = BufferFactory.Rent<char>(
            password.Length,
            isSensitive: true,
            out Span<char> chars
        );

        var status = Utf8.ToUtf16(
            password,
            chars,
            out _,
            out var charCount,
            replaceInvalidSequences: false
        );
        if (status != OperationStatus.Done)
        {
            return null;
        }

        ReadOnlySpan<char> submitted = chars[..charCount];
        if (submitted.IsNormalized(NormalizationForm.FormC))
        {
            return null;
        }

        var capacity = submitted.GetNormalizedLength(NormalizationForm.FormC);
        while (true)
        {
            using var normalizedLease = BufferFactory.Rent<char>(
                capacity,
                isSensitive: true,
                out Span<char> buffer
            );

            if (!submitted.TryNormalize(buffer, out var written, NormalizationForm.FormC))
            {
                capacity *= 2;
                continue;
            }

            ReadOnlySpan<char> normalizedChars = buffer[..written];
            var byteCount = SecureEncoding.UTF8.GetByteCount(normalizedChars);
            var bytesLease = BufferFactory.Rent(
                byteCount,
                isSensitive: true,
                out Memory<byte> bytes
            );
            SecureEncoding.UTF8.GetBytes(normalizedChars, bytes.Span);
            normalized = bytes;
            return bytesLease;
        }
    }

    private string HashCore(ReadOnlySpan<byte> password)
    {
        var iterations = Options.Iterations;

        Span<byte> salt = stackalloc byte[Options.SaltByteCount];
        Span<byte> hash = stackalloc byte[Options.HashByteCount];
        try
        {
            RandomNumberGenerator.Fill(salt);
            Rfc2898DeriveBytes.Pbkdf2(password, salt, hash, iterations, Algorithm);

            return Format(iterations, salt, hash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hash);
        }
    }

    private PasswordVerificationResult VerifyCore(
        string hashedPassword,
        ReadOnlySpan<byte> password
    )
    {
        if (!TryParse(hashedPassword, out var iterations, out var salt, out var expectedHash))
        {
            // A stored hash this hasher cannot read takes as long to refuse as a wrong password. The dummy hash always
            // reads, so this recursion is one level deep. See ADR-0239.
            _ = VerifyCore(DummyPasswordHash, password);
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

    private static bool TryParse(
        string hashedPassword,
        out int iterations,
        [NotNullWhen(true)] out byte[]? salt,
        [NotNullWhen(true)] out byte[]? expectedHash
    )
    {
        iterations = 0;
        salt = null;
        expectedHash = null;

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
                out iterations
            )
            || iterations < 1
        )
        {
            return false;
        }

        try
        {
            salt = Base64Url.DecodeFromChars(segments[3]);
            expectedHash = Base64Url.DecodeFromChars(segments[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (expectedHash.Length is < 16 or > MaxByteCount)
        {
            return false;
        }

        return true;
    }
}
