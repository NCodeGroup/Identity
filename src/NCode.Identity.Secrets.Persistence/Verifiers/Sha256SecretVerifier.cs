// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

/// <summary>
/// Provides a <see cref="SecretVerifier"/> that holds the SHA-256 digest of a secret the server generated.
/// </summary>
// See ADR-1391.
internal sealed class Sha256SecretVerifier(KeyMetadata metadata, byte[] digest) : SecretVerifier
{
    private byte[] Digest { get; } = digest;

    /// <inheritdoc />
    public override KeyMetadata Metadata { get; } = metadata;

    /// <inheritdoc />
    public override string Scheme => SecretVerifierSchemes.Sha256;

    /// <inheritdoc />
    public override bool Verify(ReadOnlySpan<byte> secret)
    {
        // Both digests are 32 bytes, so the comparison takes the same time for every presented secret.
        Span<byte> computed = stackalloc byte[SHA256.HashSizeInBytes];
        try
        {
            SHA256.HashData(secret, computed);
            return CryptographicOperations.FixedTimeEquals(computed, Digest);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(computed);
        }
    }
}
