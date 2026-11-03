// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Buffers;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.OpenId.Core.Tests.Clients.Handlers;

/// <summary>
/// Provides a verifier that confirms one secret, as a stored one-way verifier does, and counts its verifications. A
/// verifier with a work factor answers <see cref="SecretVerificationResult.Unavailable"/> when it is created
/// unavailable, as one refused a turn does.
/// </summary>
internal sealed class FakeSecretVerifier(
    string keyId,
    string secret,
    bool hasWorkFactor = false,
    bool isUnavailable = false
) : SecretVerifier
{
    private byte[] Secret { get; } = SecureEncoding.UTF8.GetBytes(secret);

    private bool IsUnavailable { get; } = isUnavailable;

    public int VerifyCount { get; private set; }

    public override KeyMetadata Metadata { get; } = new() { KeyId = keyId };

    public override string Scheme =>
        HasWorkFactor ? SecretVerifierSchemes.Pbkdf2Sha256 : SecretVerifierSchemes.Sha256;

    public override bool HasWorkFactor { get; } = hasWorkFactor;

    public override ValueTask<SecretVerificationResult> VerifyAsync(
        ReadOnlyMemory<byte> secret,
        CancellationToken cancellationToken
    )
    {
        VerifyCount++;
        if (IsUnavailable)
        {
            return ValueTask.FromResult(SecretVerificationResult.Unavailable);
        }

        return ValueTask.FromResult(
            secret.Span.SequenceEqual(Secret)
                ? SecretVerificationResult.Success
                : SecretVerificationResult.Failed
        );
    }
}
