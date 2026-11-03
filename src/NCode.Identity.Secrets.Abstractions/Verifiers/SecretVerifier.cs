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
    /// Determines whether the specified secret is the secret this verifier was made from. The comparison takes the same
    /// time wherever the presented secret first differs. A verifier with a work factor may wait for its turn before it
    /// computes, so the costly verifications that run at once stay bounded.
    /// </summary>
    /// <param name="secret">The octets of the presented secret, such as the UTF-8 bytes of a <c>client_secret</c>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous
    /// operation, including the wait for its turn.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation, containing
    /// <see langword="true"/> when <paramref name="secret"/> matches; otherwise, <see langword="false"/>.</returns>
    public abstract ValueTask<bool> VerifyAsync(
        ReadOnlyMemory<byte> secret,
        CancellationToken cancellationToken
    );

    /// <inheritdoc />
    public override string ToString() =>
        $"{nameof(SecretVerifier)} {{ KeyId = {(KeyId is null ? "(null)" : $"'{KeyId}'")}, Scheme = '{Scheme}' }}";
}
