// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

/// <summary>
/// Provides the <see cref="SecretVerifierSchemes.Pbkdf2Sha256"/> scheme,
/// <c>$ncode-pbkdf2-sha256$&lt;iterations&gt;$&lt;salt&gt;$&lt;derived key&gt;</c> with the salt and the derived key
/// base64url-encoded: the format and the parameters of the family's password hashes. It verifies a secret someone
/// chose, whose entropy is unknown. Every verifier it reads shares one gate, so at most
/// <see cref="Pbkdf2SecretVerifierOptions.MaxConcurrency"/> derivations run at once in the process.
/// </summary>
// See ADR-1391.
internal sealed class Pbkdf2SecretVerifierScheme : ISecretVerifierScheme, IDisposable
{
    /// <summary>
    /// Gets the length, in bytes, of the random salt of a new verifier, as the family's password hashes use.
    /// </summary>
    public const int SaltByteCount = 16;

    /// <summary>
    /// Gets the length, in bytes, of the derived key of a new verifier, as the family's password hashes use.
    /// </summary>
    public const int DerivedKeyByteCount = 32;

    private const int MinSaltByteCount = 8;
    private const int MinDerivedKeyByteCount = 16;
    private const int MaxByteCount = 256;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pbkdf2SecretVerifierScheme"/> class.
    /// </summary>
    /// <param name="optionsAccessor">The accessor of the <see cref="Pbkdf2SecretVerifierOptions"/>.</param>
    public Pbkdf2SecretVerifierScheme(IOptions<Pbkdf2SecretVerifierOptions> optionsAccessor)
    {
        var options = optionsAccessor.Value;
        Iterations = options.Iterations;

        var maxConcurrency = options.MaxConcurrency;
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1, nameof(optionsAccessor));
        Gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    private int Iterations { get; }

    /// <summary>
    /// Gets the gate that every verifier the scheme reads waits on for its turn.
    /// </summary>
    internal SemaphoreSlim Gate { get; }

    /// <inheritdoc />
    public string Name => SecretVerifierSchemes.Pbkdf2Sha256;

    /// <inheritdoc />
    public void Dispose() => Gate.Dispose();

    /// <inheritdoc />
    public void WriteParameters(ReadOnlySpan<byte> secret, IBufferWriter<byte> destination)
    {
        var iterations = Iterations;
        if (iterations < 1)
        {
            throw new InvalidOperationException(
                $"'{nameof(Pbkdf2SecretVerifierOptions)}.{nameof(Pbkdf2SecretVerifierOptions.Iterations)}' is "
                    + $"{iterations}; it must be at least 1."
            );
        }

        Span<byte> salt = stackalloc byte[SaltByteCount];
        Span<byte> derivedKey = stackalloc byte[DerivedKeyByteCount];
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

            var iterationsText = destination.GetSpan(11);
            if (!Utf8Formatter.TryFormat(iterations, iterationsText, out var iterationsLength))
            {
                throw new InvalidOperationException("The iteration count could not be formatted.");
            }

            destination.Advance(iterationsLength);
            SecretVerifierFormat.WriteSeparator(destination);
            SecretVerifierFormat.WriteBase64Url(salt, destination);
            SecretVerifierFormat.WriteSeparator(destination);
            SecretVerifierFormat.WriteBase64Url(derivedKey, destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    /// <inheritdoc />
    public bool TryReadParameters(
        KeyMetadata metadata,
        ReadOnlySpan<byte> parameters,
        [NotNullWhen(true)] out SecretVerifier? verifier
    )
    {
        verifier = null;

        var iterationsEnd = parameters.IndexOf(SecretVerifierFormat.Separator);
        if (iterationsEnd <= 0)
        {
            return false;
        }

        var iterationsText = parameters[..iterationsEnd];
        if (
            !SecretVerifierFormat.IsDigits(iterationsText)
            || !Utf8Parser.TryParse(iterationsText, out int iterations, out var consumed)
            || consumed != iterationsText.Length
            || iterations < 1
        )
        {
            return false;
        }

        var rest = parameters[(iterationsEnd + 1)..];
        var saltEnd = rest.IndexOf(SecretVerifierFormat.Separator);
        if (saltEnd < 0)
        {
            return false;
        }

        if (
            !SecretVerifierFormat.TryDecode(rest[..saltEnd], out var salt)
            || salt.Length is < MinSaltByteCount or > MaxByteCount
            || !SecretVerifierFormat.TryDecode(rest[(saltEnd + 1)..], out var derivedKey)
            || derivedKey.Length is < MinDerivedKeyByteCount or > MaxByteCount
        )
        {
            return false;
        }

        verifier = new Pbkdf2SecretVerifier(metadata, iterations, salt, derivedKey, Gate);
        return true;
    }
}
