// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Represents an <see cref="AsymmetricSecretKey"/> implementation using <c>Elliptic-Curve</c> cryptographic keys.
/// </summary>
/// <param name="metadata">The metadata of the key.</param>
/// <param name="curveName">The <c>JWK</c> <c>crv</c> name of the key's curve, as the elliptic-curve catalog names it,
/// or <see langword="null"/> when the catalog does not know the curve.</param>
/// <param name="curveSizeBits">The size of the key's curve, in bits.</param>
/// <param name="certificateRawData">The optional DER-encoded certificate of the key, or <see langword="null"/>.</param>
/// <param name="issuerCertificatesRawData">The DER-encoded certificates of the authorities that issued the key's
/// certificate, or <see langword="null"/> for none.</param>
[PublicAPI]
public abstract class EccSecretKey(
    KeyMetadata metadata,
    string? curveName,
    int curveSizeBits,
    ReadOnlyMemory<byte>? certificateRawData,
    IReadOnlyList<ReadOnlyMemory<byte>>? issuerCertificatesRawData = null
) : AsymmetricSecretKey(metadata, curveSizeBits, certificateRawData, issuerCertificatesRawData)
{
    /// <summary>
    /// OID for <c>Elliptic-Curve</c> public key cryptography.
    /// </summary>
    public const string Oid = "1.2.840.10045.2.1";

    // A boxed ECCurve: the reference is published atomically with Interlocked, and the box is never mutated.
    private object? _curve;

    /// <inheritdoc />
    public override string KeyType => SecretKeyTypes.Ecc;

    /// <inheritdoc />
    public override string? CurveName { get; } = curveName;

    /// <summary>
    /// Gets the <see cref="ECCurve"/> for the current <c>Elliptic-Curve</c> key material.
    /// </summary>
    /// <returns>The <see cref="ECCurve"/> for the current <c>Elliptic-Curve</c> key material.</returns>
    /// <remarks>
    /// The curve is read from the key material, so two curves of the same size are told apart,
    /// and is read once per key instance.
    /// </remarks>
    public virtual ECCurve GetECCurve()
    {
        if (Volatile.Read(ref _curve) is ECCurve cached)
            return cached;

        using var key = ExportECDsa();
        var curve = key.ExportParameters(includePrivateParameters: false).Curve;
        return Interlocked.CompareExchange(ref _curve, curve, null) is ECCurve existing
            ? existing
            : curve;
    }

    /// <summary>
    /// Factory method to create an <see cref="ECDsa"/> instance from the current <c>Elliptic-Curve</c> key material.
    /// </summary>
    /// <returns>The newly created <see cref="ECDsa"/> instance</returns>
    public abstract ECDsa ExportECDsa();

    /// <summary>
    /// Factory method to create an <see cref="ECDiffieHellman"/> instance from the current <c>Elliptic-Curve</c> key
    /// material.
    /// </summary>
    /// <returns>The newly created <see cref="ECDiffieHellman"/> instance</returns>
    public abstract ECDiffieHellman ExportECDiffieHellman();

    /// <inheritdoc />
    public override void ExportSubjectPublicKeyInfo<TWriter>(ref TWriter destination)
    {
        using var key = ExportECDsa();
        WriteSubjectPublicKeyInfo(key, ref destination);
    }
}
