// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

/// <summary>
/// Provides the verifier <see cref="PlainTextSecretVerifierScheme"/> reads, which compares the presented secret with
/// the stored one; only a test would store it.
/// </summary>
internal sealed class PlainTextSecretVerifier(KeyMetadata metadata, byte[] secret) : SecretVerifier
{
    public override KeyMetadata Metadata { get; } = metadata;

    public override string Scheme => PlainTextSecretVerifierScheme.SchemeName;

    public override bool HasWorkFactor => false;

    public override ValueTask<SecretVerificationResult> VerifyAsync(
        ReadOnlyMemory<byte> presented,
        CancellationToken cancellationToken
    ) =>
        ValueTask.FromResult(
            presented.Span.SequenceEqual(secret)
                ? SecretVerificationResult.Success
                : SecretVerificationResult.Failed
        );
}
