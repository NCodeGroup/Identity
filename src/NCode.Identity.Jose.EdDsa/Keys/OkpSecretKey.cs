// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using JetBrains.Annotations;
using NCode.Identity.Jose.EdDsa.KeyTypes;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.EdDsa.Keys;

/// <summary>
/// Represents an <c>OKP</c> (Octet Key Pair) Edwards-curve secret key used for <c>EdDSA</c> (RFC 8037) — either
/// Ed25519 or Ed448. The key material is the raw public key (always present) and, for a private key, the raw seed
/// (exposed only through <see cref="ExportPrivateKey{TWriter}"/>). OKP keys have no
/// <see cref="System.Security.Cryptography.AsymmetricAlgorithm"/> representation; their public material is produced
/// as <c>SubjectPublicKeyInfo</c> DER (RFC 8410).
/// </summary>
/// <param name="metadata">The <see cref="KeyMetadata"/> for the key.</param>
/// <param name="curve">The JWK <c>crv</c> name of the key: <c>Ed25519</c> or <c>Ed448</c>.</param>
/// <param name="keySizeBits">The size, in bits, of the raw public key.</param>
/// <param name="publicKey">The raw public key bytes (32 for Ed25519, 57 for Ed448).</param>
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

    /// <summary>Gets the JWK <c>crv</c> name of the key: <c>Ed25519</c> or <c>Ed448</c>.</summary>
    public string Curve { get; } = curve;

    /// <summary>Gets a value indicating whether this key holds the private seed (and can therefore sign).</summary>
    public abstract bool HasPrivateKey { get; }

    /// <summary>Writes the raw public key bytes (32 for Ed25519, 57 for Ed448) to the destination.</summary>
    public void ExportPublicKey<TWriter>(ref TWriter destination)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var source = _publicKey.Span;
        var span = destination.GetSpan(source.Length);
        source.CopyTo(span);
        destination.Advance(source.Length);
    }

    /// <summary>Writes the raw private seed to the destination. Throws when this key holds only the public half.</summary>
    public abstract void ExportPrivateKey<TWriter>(ref TWriter destination)
        where TWriter : IBufferWriter<byte>, allows ref struct;

    /// <inheritdoc />
    public override void ExportSubjectPublicKeyInfo<TWriter>(ref TWriter destination)
    {
        if (!OkpCurves.TryGetByName(Curve, out var curve))
        {
            throw new InvalidOperationException($"The OKP curve '{Curve}' is not supported.");
        }

        OkpKeyEncoder.WriteSubjectPublicKeyInfo(curve, _publicKey.Span, ref destination);
    }
}
