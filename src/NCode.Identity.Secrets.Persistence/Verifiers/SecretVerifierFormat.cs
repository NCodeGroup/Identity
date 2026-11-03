// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

/// <summary>
/// Makes and reads the self-describing text of a stored <see cref="SecretVerifier"/>, <c>$&lt;scheme&gt;$…</c>, as
/// ASCII octets that are protected and stored like key material. The text names its scheme and the scheme's
/// parameters, so a verifier keeps verifying when the scheme or the parameters chosen for new secrets change.
/// </summary>
// See ADR-1391.
internal static class SecretVerifierFormat
{
    /// <summary>
    /// Gets the length, in bytes, of the random salt of a PBKDF2 verifier, as the family's password hashes use.
    /// </summary>
    public const int Pbkdf2SaltByteCount = 16;

    /// <summary>
    /// Gets the length, in bytes, of the derived key of a PBKDF2 verifier, as the family's password hashes use.
    /// </summary>
    public const int Pbkdf2DerivedKeyByteCount = 32;

    private const int MinPbkdf2SaltByteCount = 8;
    private const int MinPbkdf2DerivedKeyByteCount = 16;
    private const int MaxPbkdf2ByteCount = 256;
    private const byte Separator = (byte)'$';

    private static ReadOnlySpan<byte> Sha256Scheme => "ncode-sha256"u8;
    private static ReadOnlySpan<byte> Pbkdf2Scheme => "ncode-pbkdf2-sha256"u8;

    /// <summary>
    /// Makes the SHA-256 verifier of a secret, <c>$ncode-sha256$&lt;digest&gt;</c>.
    /// </summary>
    /// <param name="secret">The octets of the secret.</param>
    /// <param name="verifier">The verifier text, in the returned sensitive buffer.</param>
    /// <returns>The lease of the sensitive buffer that holds <paramref name="verifier"/>.</returns>
    public static IMemoryOwner<byte> CreateSha256(
        ReadOnlySpan<byte> secret,
        out ReadOnlyMemory<byte> verifier
    )
    {
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        try
        {
            SHA256.HashData(secret, digest);

            var length =
                1 + Sha256Scheme.Length + 1 + Base64Url.GetEncodedLength(SHA256.HashSizeInBytes);
            var lease = BufferFactory.Rent(length, isSensitive: true, out Memory<byte> buffer);

            var destination = buffer.Span;
            var position = WriteScheme(destination, Sha256Scheme);
            position += Base64Url.EncodeToUtf8(digest, destination[position..]);

            verifier = buffer[..position];
            return lease;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    /// <summary>
    /// Makes the PBKDF2-HMAC-SHA256 verifier of a secret with a fresh random salt,
    /// <c>$ncode-pbkdf2-sha256$&lt;iterations&gt;$&lt;salt&gt;$&lt;derived key&gt;</c>.
    /// </summary>
    /// <param name="secret">The octets of the secret.</param>
    /// <param name="iterations">The number of PBKDF2 iterations.</param>
    /// <param name="verifier">The verifier text, in the returned sensitive buffer.</param>
    /// <returns>The lease of the sensitive buffer that holds <paramref name="verifier"/>.</returns>
    public static IMemoryOwner<byte> CreatePbkdf2(
        ReadOnlySpan<byte> secret,
        int iterations,
        out ReadOnlyMemory<byte> verifier
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);

        Span<byte> salt = stackalloc byte[Pbkdf2SaltByteCount];
        Span<byte> derivedKey = stackalloc byte[Pbkdf2DerivedKeyByteCount];
        Span<byte> iterationsText = stackalloc byte[11];
        try
        {
            RandomNumberGenerator.Fill(salt);
            Rfc2898DeriveBytes.Pbkdf2(
                secret,
                salt,
                derivedKey,
                iterations,
                HashAlgorithmName.SHA256
            );

            var formatted = Utf8Formatter.TryFormat(
                iterations,
                iterationsText,
                out var iterationsLength
            );
            if (!formatted)
            {
                throw new InvalidOperationException("The iteration count could not be formatted.");
            }

            var length =
                1
                + Pbkdf2Scheme.Length
                + 1
                + iterationsLength
                + 1
                + Base64Url.GetEncodedLength(Pbkdf2SaltByteCount)
                + 1
                + Base64Url.GetEncodedLength(Pbkdf2DerivedKeyByteCount);
            var lease = BufferFactory.Rent(length, isSensitive: true, out Memory<byte> buffer);

            var destination = buffer.Span;
            var position = WriteScheme(destination, Pbkdf2Scheme);
            iterationsText[..iterationsLength].CopyTo(destination[position..]);
            position += iterationsLength;
            destination[position++] = Separator;
            position += Base64Url.EncodeToUtf8(salt, destination[position..]);
            destination[position++] = Separator;
            position += Base64Url.EncodeToUtf8(derivedKey, destination[position..]);

            verifier = buffer[..position];
            return lease;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    /// <summary>
    /// Reads the text of a stored verifier.
    /// </summary>
    /// <param name="metadata">The metadata of the secret the verifier confirms.</param>
    /// <param name="text">The verifier text.</param>
    /// <param name="verifier">When this method returns <see langword="true"/>, the verifier; otherwise,
    /// <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is a verifier of a known scheme with valid
    /// parameters; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(
        KeyMetadata metadata,
        ReadOnlySpan<byte> text,
        [NotNullWhen(true)] out SecretVerifier? verifier
    )
    {
        verifier = null;

        if (text.IsEmpty || text[0] != Separator)
        {
            return false;
        }

        var rest = text[1..];
        var schemeEnd = rest.IndexOf(Separator);
        if (schemeEnd < 0)
        {
            return false;
        }

        var scheme = rest[..schemeEnd];
        var parameters = rest[(schemeEnd + 1)..];

        if (scheme.SequenceEqual(Sha256Scheme))
        {
            return TryParseSha256(metadata, parameters, out verifier);
        }

        if (scheme.SequenceEqual(Pbkdf2Scheme))
        {
            return TryParsePbkdf2(metadata, parameters, out verifier);
        }

        return false;
    }

    private static bool TryParseSha256(
        KeyMetadata metadata,
        ReadOnlySpan<byte> parameters,
        [NotNullWhen(true)] out SecretVerifier? verifier
    )
    {
        verifier = null;

        if (!TryDecode(parameters, out var digest) || digest.Length != SHA256.HashSizeInBytes)
        {
            return false;
        }

        verifier = new Sha256SecretVerifier(metadata, digest);
        return true;
    }

    private static bool TryParsePbkdf2(
        KeyMetadata metadata,
        ReadOnlySpan<byte> parameters,
        [NotNullWhen(true)] out SecretVerifier? verifier
    )
    {
        verifier = null;

        var iterationsEnd = parameters.IndexOf(Separator);
        if (iterationsEnd <= 0)
        {
            return false;
        }

        var iterationsText = parameters[..iterationsEnd];
        if (
            !IsDigits(iterationsText)
            || !Utf8Parser.TryParse(iterationsText, out int iterations, out var consumed)
            || consumed != iterationsText.Length
            || iterations < 1
        )
        {
            return false;
        }

        var rest = parameters[(iterationsEnd + 1)..];
        var saltEnd = rest.IndexOf(Separator);
        if (saltEnd < 0)
        {
            return false;
        }

        if (
            !TryDecode(rest[..saltEnd], out var salt)
            || salt.Length is < MinPbkdf2SaltByteCount or > MaxPbkdf2ByteCount
            || !TryDecode(rest[(saltEnd + 1)..], out var derivedKey)
            || derivedKey.Length is < MinPbkdf2DerivedKeyByteCount or > MaxPbkdf2ByteCount
        )
        {
            return false;
        }

        verifier = new Pbkdf2SecretVerifier(metadata, iterations, salt, derivedKey);
        return true;
    }

    private static bool IsDigits(ReadOnlySpan<byte> text)
    {
        foreach (var value in text)
        {
            if (value is < (byte)'0' or > (byte)'9')
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryDecode(ReadOnlySpan<byte> text, [NotNullWhen(true)] out byte[]? bytes)
    {
        bytes = null;

        if (text.IsEmpty || text.Contains(Separator) || !Base64Url.IsValid(text))
        {
            return false;
        }

        var decoded = new byte[Base64Url.GetMaxDecodedLength(text.Length)];
        if (!Base64Url.TryDecodeFromUtf8(text, decoded, out var written))
        {
            return false;
        }

        bytes = written == decoded.Length ? decoded : decoded[..written];
        return true;
    }

    // Writes '$<scheme>$' and returns the number of bytes written.
    private static int WriteScheme(Span<byte> destination, ReadOnlySpan<byte> scheme)
    {
        destination[0] = Separator;
        scheme.CopyTo(destination[1..]);
        destination[1 + scheme.Length] = Separator;
        return scheme.Length + 2;
    }
}
