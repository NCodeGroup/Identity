// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using JetBrains.Annotations;
using NCode.Identity.Jose.EdDsa.KeyTypes;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.EdDsa.Keys;

/// <summary>
/// Represents an <c>OKP</c> (Octet Key Pair) secret key (RFC 8037): an Ed25519 or Ed448 key, which signs with
/// <c>EdDSA</c>, or an X25519 or X448 key, which agrees keys with <c>ECDH-ES</c>. Its <see cref="CurveName"/> decides
/// which, and a key of one use is never accepted for the other. The key material is the raw public key (always present)
/// and, for a private key, the raw private key (the seed of an Edwards curve, the scalar of a Montgomery curve),
/// exposed only through <see cref="ExportPrivateKey{TWriter}"/>. OKP keys have no
/// <see cref="System.Security.Cryptography.AsymmetricAlgorithm"/> representation; their public material is produced
/// as <c>SubjectPublicKeyInfo</c> DER (RFC 8410).
/// </summary>
/// <param name="metadata">The <see cref="KeyMetadata"/> for the key.</param>
/// <param name="curve">The JWK <c>crv</c> name of the key: <c>Ed25519</c>, <c>Ed448</c>, <c>X25519</c> or
/// <c>X448</c>.</param>
/// <param name="keySizeBits">The size, in bits, of the raw public key.</param>
/// <param name="publicKey">The raw public key bytes (32 for Ed25519 and X25519, 57 for Ed448, 56 for X448).</param>
/// <param name="certificateRawData">The optional DER-encoded certificate bytes, or <see langword="null"/>.</param>
[PublicAPI]
public abstract class OkpSecretKey(
    KeyMetadata metadata,
    string curve,
    int keySizeBits,
    ReadOnlyMemory<byte> publicKey,
    ReadOnlyMemory<byte>? certificateRawData
) : AsymmetricSecretKey(metadata, keySizeBits, certificateRawData)
{
    /// <summary>The <see cref="SecretKey.KeyType"/> value for an OKP key.</summary>
    public const string TypeName = "okp";

    private readonly ReadOnlyMemory<byte> _publicKey = publicKey;

    /// <inheritdoc />
    public override string KeyType => TypeName;

    /// <summary>
    /// Gets the JWK <c>crv</c> name of the key: <c>Ed25519</c>, <c>Ed448</c>, <c>X25519</c> or <c>X448</c>.
    /// </summary>
    public override string CurveName { get; } = curve;

    /// <summary>
    /// Gets a value indicating whether this key holds the private key, and can therefore sign or, on a key-agreement
    /// curve, agree keys as a recipient.
    /// </summary>
    public abstract bool HasPrivateKey { get; }

    /// <summary>
    /// Writes the raw public key bytes (32 for Ed25519 and X25519, 57 for Ed448, 56 for X448) to the destination.
    /// </summary>
    public void ExportPublicKey<TWriter>(ref TWriter destination)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var source = _publicKey.Span;
        var span = destination.GetSpan(source.Length);
        source.CopyTo(span);
        destination.Advance(source.Length);
    }

    /// <summary>
    /// Writes the raw private key (the seed of an Edwards curve, the scalar of a Montgomery curve) to the destination.
    /// Throws when this key holds only the public half.
    /// </summary>
    public abstract void ExportPrivateKey<TWriter>(ref TWriter destination)
        where TWriter : IBufferWriter<byte>, allows ref struct;

    /// <inheritdoc />
    public override void ExportSubjectPublicKeyInfo<TWriter>(ref TWriter destination)
    {
        if (!OkpCurves.TryGetByName(CurveName, out var curve))
        {
            throw new InvalidOperationException($"The OKP curve '{CurveName}' is not supported.");
        }

        OkpKeyEncoder.WriteSubjectPublicKeyInfo(curve, _publicKey.Span, ref destination);
    }
}
