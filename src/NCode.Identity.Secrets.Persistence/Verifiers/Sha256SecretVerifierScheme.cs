// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

/// <summary>
/// Provides the <see cref="SecretVerifierSchemes.Sha256"/> scheme, <c>$ncode-sha256$&lt;digest&gt;</c> with the
/// SHA-256 digest of the secret base64url-encoded. It verifies a secret the server generated with at least 256 bits of
/// entropy, which no search can guess, so it adds no work factor.
/// </summary>
// See ADR-1391.
internal sealed class Sha256SecretVerifierScheme : ISecretVerifierScheme
{
    /// <inheritdoc />
    public string Name => SecretVerifierSchemes.Sha256;

    /// <inheritdoc />
    public void WriteParameters(ReadOnlySpan<byte> secret, IBufferWriter<byte> destination)
    {
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        try
        {
            SHA256.HashData(secret, digest);
            SecretVerifierFormat.WriteBase64Url(digest, destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
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

        if (
            !SecretVerifierFormat.TryDecode(parameters, out var digest)
            || digest.Length != SHA256.HashSizeInBytes
        )
        {
            return false;
        }

        verifier = new Sha256SecretVerifier(metadata, digest);
        return true;
    }
}
