// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Performs the Diffie-Hellman key agreement of the <c>ECDH-ES</c> key-management algorithms (RFC 7518 §4.6) over the
/// keys of one key type, such as <c>EC</c> keys (RFC 7518) or <c>OKP</c> X25519 and X448 keys (RFC 8037 §3.2). The
/// <c>ECDH-ES</c>, <c>ECDH-ES+A128KW</c>, <c>ECDH-ES+A192KW</c> and <c>ECDH-ES+A256KW</c> algorithms accept every key
/// type that has a registered agreement, so a package that contributes a key type contributes its agreement through
/// dependency injection and the algorithms serve it without changing. The algorithm writes and reads the ephemeral
/// public key (<c>epk</c>) as a JSON Web Key through the key type's <c>ISecretKeyTypeDescriptor</c>, so the agreement
/// handles key material only, never JSON.
/// </summary>
// See ADR-1389.
[PublicAPI]
public interface IEcdhKeyAgreement
{
    /// <summary>
    /// Gets the type of key this agreement serves, with the curves, or for a key type without curves the sizes, it
    /// accepts keys of that type on. A key takes part in the agreement when
    /// <see cref="Algorithms.AlgorithmKeyType.IsKeyCompatible"/> accepts it. Several agreements may serve one key type,
    /// each over its own curves, as long as no key is accepted by two of them.
    /// </summary>
    AlgorithmKeyType AlgorithmKeyType { get; }

    /// <summary>
    /// Gets the size, in bytes, of the shared secret <c>Z</c> that an agreement with the specified key produces.
    /// </summary>
    /// <param name="secretKey">The recipient's key.</param>
    /// <returns>The size, in bytes, of the shared secret.</returns>
    int GetSharedSecretSizeBytes(SecretKey secretKey);

    /// <summary>
    /// The sender's half: generates an ephemeral key pair on the curve of the recipient's key, agrees on the shared
    /// secret <c>Z</c> with the recipient's public key, and writes the ephemeral public key as a DER-encoded
    /// <c>SubjectPublicKeyInfo</c>. The ephemeral private key never leaves this method.
    /// </summary>
    /// <param name="recipientKey">The recipient's key; only its public part is used.</param>
    /// <param name="ephemeralPublicKeyWriter">The destination for the ephemeral public key as a
    /// <c>SubjectPublicKeyInfo</c>.</param>
    /// <param name="sharedSecret">The destination for the shared secret, of exactly
    /// <see cref="GetSharedSecretSizeBytes"/> bytes.</param>
    void AgreeWithEphemeralKey(
        SecretKey recipientKey,
        IBufferWriter<byte> ephemeralPublicKeyWriter,
        Span<byte> sharedSecret
    );

    /// <summary>
    /// The recipient's half: agrees on the shared secret <c>Z</c> from the recipient's private key and the sender's
    /// ephemeral public key. Throws when the ephemeral key is not on the curve of the recipient's key, or when the
    /// agreement yields a degenerate secret, such as the all-zero output of X25519 for a low-order point
    /// (RFC 7748 §6).
    /// </summary>
    /// <param name="recipientKey">The recipient's private key.</param>
    /// <param name="ephemeralPublicKey">The sender's ephemeral public key, of the same key type.</param>
    /// <param name="sharedSecret">The destination for the shared secret, of exactly
    /// <see cref="GetSharedSecretSizeBytes"/> bytes.</param>
    void Agree(SecretKey recipientKey, SecretKey ephemeralPublicKey, Span<byte> sharedSecret);
}
