// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Jose.Algorithms.Signature;

/// <summary>
/// Provides an implementation of <see cref="SignatureAlgorithm"/> that uses an <c>Elliptic-Curve (ECC)</c> cryptographic
/// algorithm for digital signatures, bound to the one curve its <see cref="EccSignatureAlgorithmSpecification"/> names.
/// </summary>
// See ADR-1272 and ADR-1392.
internal class EccSignatureAlgorithm : SignatureAlgorithm
{
    /// <inheritdoc />
    public override string Code { get; }

    /// <inheritdoc />
    public override IReadOnlyList<AlgorithmKeyType> KeyTypes { get; }

    private IReadOnlyList<KeySizes> KeyBitSizes => KeyTypes[0].KeyBitSizes;

    /// <inheritdoc />
    public override HashAlgorithmName HashAlgorithmName { get; }

    private int SignatureSizeBytes { get; }

    private ECCurve Curve { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EccSignatureAlgorithm"/> class.
    /// </summary>
    /// <param name="specification">The algorithm's code, the only elliptic curve its keys may use, and the hash function it
    /// signs with.</param>
    public EccSignatureAlgorithm(EccSignatureAlgorithmSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);

        Code = specification.Code;
        Curve = specification.Curve.Curve;
        HashAlgorithmName = specification.HashAlgorithmName;

        var keySizeBits = specification.Curve.CurveSizeBits;

        // An ECDSA signature is the pair (R, S), each the size of the curve's order rounded up to whole bytes
        // (RFC 7518 §3.4).
        SignatureSizeBytes = 2 * ((keySizeBits + 7) / 8);

        KeyTypes =
        [
            AlgorithmKeyType.OnCurves(
                typeof(EccSecretKey),
                [new AlgorithmCurve(specification.Curve.CurveName, keySizeBits)]
            ),
        ];
    }

    /// <inheritdoc />
    public override int GetSignatureSizeBytes(int keySizeBits) => SignatureSizeBytes;

    // The key's curve is checked again when it is used, by the curve itself rather than its name.
    private void EnsureCurve(ECDsa key)
    {
        if (
            !EccCurveSpecification.IsSameNamedCurve(
                key.ExportParameters(includePrivateParameters: false).Curve,
                Curve
            )
        )
        {
            // the argument that is invalid is the caller's secretKey, not this helper's ECDsa parameter
#pragma warning disable CA2208
            throw new ArgumentException(
                "The secret key's elliptic curve is not valid for this cryptographic algorithm.",
                "secretKey"
            );
#pragma warning restore CA2208
        }
    }

    /// <inheritdoc />
    public override bool TrySign(
        SecretKey secretKey,
        ReadOnlySpan<byte> inputData,
        Span<byte> signature,
        out int bytesWritten
    )
    {
        var validatedSecurityKey = secretKey.Validate<EccSecretKey>(KeyBitSizes);

        using var key = validatedSecurityKey.ExportECDsa();
        EnsureCurve(key);

        return key.TrySignData(inputData, signature, HashAlgorithmName, out bytesWritten);
    }

    /// <inheritdoc />
    public override bool Verify(
        SecretKey secretKey,
        ReadOnlySpan<byte> inputData,
        ReadOnlySpan<byte> signature
    )
    {
        var validatedSecurityKey = secretKey.Validate<EccSecretKey>(KeyBitSizes);

        using var key = validatedSecurityKey.ExportECDsa();
        EnsureCurve(key);

        return key.VerifyData(inputData, signature, HashAlgorithmName);
    }
}
