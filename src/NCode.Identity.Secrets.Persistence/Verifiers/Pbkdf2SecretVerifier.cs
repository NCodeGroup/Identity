// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

/// <summary>
/// Provides a <see cref="SecretVerifier"/> that holds a PBKDF2-HMAC-SHA256 derivation of a secret someone chose. A
/// verification waits for a turn of the gate its scheme shares among every verifier it reads, so the derivations that
/// run at once stay bounded however many presented secrets arrive.
/// </summary>
// See ADR-1391.
internal sealed class Pbkdf2SecretVerifier(
    KeyMetadata metadata,
    int iterations,
    byte[] salt,
    byte[] derivedKey,
    SemaphoreSlim gate
) : SecretVerifier
{
    private int Iterations { get; } = iterations;
    private byte[] Salt { get; } = salt;
    private byte[] DerivedKey { get; } = derivedKey;
    private SemaphoreSlim Gate { get; } = gate;

    /// <inheritdoc />
    public override KeyMetadata Metadata { get; } = metadata;

    /// <inheritdoc />
    public override string Scheme => SecretVerifierSchemes.Pbkdf2Sha256;

    /// <inheritdoc />
    public override async ValueTask<bool> VerifyAsync(
        ReadOnlyMemory<byte> secret,
        CancellationToken cancellationToken
    )
    {
        // Anyone who knows a client_id can make the server run a derivation, so a derivation waits for a turn rather
        // than each request taking a processor of its own.
        await Gate.WaitAsync(cancellationToken);
        try
        {
            return Verify(secret.Span);
        }
        finally
        {
            Gate.Release();
        }
    }

    private bool Verify(ReadOnlySpan<byte> secret)
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
