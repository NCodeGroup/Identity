// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

/// <summary>
/// Provides a <see cref="SecretVerifier"/> that holds a PBKDF2-HMAC-SHA256 derivation of a secret someone chose.
/// </summary>
// See ADR-1391.
internal sealed class Pbkdf2SecretVerifier(
    KeyMetadata metadata,
    int iterations,
    byte[] salt,
    byte[] derivedKey
) : SecretVerifier
{
    private int Iterations { get; } = iterations;
    private byte[] Salt { get; } = salt;
    private byte[] DerivedKey { get; } = derivedKey;

    /// <inheritdoc />
    public override KeyMetadata Metadata { get; } = metadata;

    /// <inheritdoc />
    public override string Scheme => SecretVerifierSchemes.Pbkdf2Sha256;

    /// <inheritdoc />
    public override bool Verify(ReadOnlySpan<byte> secret)
    {
        // The derived key has the stored key's length whatever the presented secret is, so the comparison takes the
        // same time for every presented secret.
        Span<byte> computed = stackalloc byte[DerivedKey.Length];
        try
        {
            Rfc2898DeriveBytes.Pbkdf2(secret, Salt, computed, Iterations, HashAlgorithmName.SHA256);
            return CryptographicOperations.FixedTimeEquals(computed, DerivedKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(computed);
        }
    }
}
