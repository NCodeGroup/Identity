// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Verifiers;

/// <summary>
/// Represents the one-way verifier of a shared secret, such as a <c>client_secret</c> that is only ever compared: it
/// confirms that a presented secret is the secret it was made from, and that secret cannot be read back from it. A
/// verifier is not a key; nothing is signed, authenticated, or encrypted with it.
/// </summary>
// See ADR-1391.
[PublicAPI]
public abstract class SecretVerifier
{
    /// <summary>
    /// Gets the metadata of the secret this verifier confirms.
    /// </summary>
    public abstract KeyMetadata Metadata { get; }

    /// <summary>
    /// Gets the identifier of the secret this verifier confirms.
    /// </summary>
    public string? KeyId => Metadata.KeyId;

    /// <summary>
    /// Gets the name of the scheme the verifier was made with. See <see cref="SecretVerifierSchemes"/> for the
    /// schemes the family makes.
    /// </summary>
    public abstract string Scheme { get; }

    /// <summary>
    /// Gets a value indicating whether the verifier carries a work factor, such as a password hash's iterations, so that
    /// a verification costs far more than the request that presents the secret. Such a verification waits for its turn,
    /// and a caller compares a presented secret with every verifier that has no work factor before it tries one that
    /// does.
    /// </summary>
    public abstract bool HasWorkFactor { get; }

    /// <summary>
    /// Compares the specified secret with the secret this verifier was made from. The comparison takes the same time
    /// wherever the presented secret first differs. A verifier with a work factor waits for its turn before it
    /// computes, so the costly verifications that run at once stay bounded, and answers
    /// <see cref="SecretVerificationResult.Unavailable"/> without comparing when no turn comes soon enough.
    /// </summary>
    /// <param name="secret">The octets of the presented secret, such as the UTF-8 bytes of a <c>client_secret</c>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous
    /// operation, including the wait for its turn.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation, containing
    /// <see cref="SecretVerificationResult.Success"/> when <paramref name="secret"/> matches,
    /// <see cref="SecretVerificationResult.Failed"/> when it does not, and
    /// <see cref="SecretVerificationResult.Unavailable"/> when it was not compared.</returns>
    public abstract ValueTask<SecretVerificationResult> VerifyAsync(
        ReadOnlyMemory<byte> secret,
        CancellationToken cancellationToken
    );

    /// <inheritdoc />
    public override string ToString() =>
        $"{nameof(SecretVerifier)} {{ KeyId = {(KeyId is null ? "(null)" : $"'{KeyId}'")}, Scheme = '{Scheme}' }}";
}
