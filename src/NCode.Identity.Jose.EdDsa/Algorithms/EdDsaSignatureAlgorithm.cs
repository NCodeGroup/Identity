#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.EdDsa.Keys;
using NCode.Identity.Jose.EdDsa.KeyTypes;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.EdDsa.Algorithms;

/// <summary>
/// The <c>EdDSA</c> (RFC 8037) <see cref="SignatureAlgorithm"/>, registered under three <c>JOSE</c> algorithm codes:
/// the polymorphic <c>EdDSA</c>, which selects the concrete curve (Ed25519 or Ed448) from the key's
/// <see cref="OkpSecretKey.Curve"/> at signing and verification time, and the fully-specified <c>Ed25519</c> and
/// <c>Ed448</c>, each of which accepts only keys on its own curve.
/// </summary>
/// <param name="code">The <c>JOSE</c> algorithm code.</param>
/// <param name="curve">The curve the algorithm is fixed to, or <see langword="null"/> to accept either curve.</param>
internal sealed class EdDsaSignatureAlgorithm(string code, OkpCurveInfo? curve) : SignatureAlgorithm
{
    private OkpCurveInfo? Curve { get; } = curve;

    /// <inheritdoc />
    public override string Code { get; } = code;

    /// <inheritdoc />
    public override Type KeyType => typeof(OkpSecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KeyBitSizes { get; } =
        curve is not null
            ? [new KeySizes(minSize: curve.KeySizeBits, maxSize: curve.KeySizeBits, skipSize: 0)]
            :
            [
                new KeySizes(
                    minSize: OkpCurves.Ed25519KeySizeBits,
                    maxSize: OkpCurves.Ed25519KeySizeBits,
                    skipSize: 0
                ),
                new KeySizes(
                    minSize: OkpCurves.Ed448KeySizeBits,
                    maxSize: OkpCurves.Ed448KeySizeBits,
                    skipSize: 0
                ),
            ];

    private bool TryResolveCurve(OkpSecretKey key, [NotNullWhen(true)] out OkpCurveInfo? resolved)
    {
        if (OkpCurves.TryGetByName(key.Curve, out resolved) && (Curve is null || resolved == Curve))
        {
            return true;
        }

        resolved = null;
        return false;
    }

    /// <inheritdoc />
    public override bool IsKeyCompatible(SecretKey secretKey) =>
        base.IsKeyCompatible(secretKey) && TryResolveCurve((OkpSecretKey)secretKey, out _);

    /// <inheritdoc />
    public override HashFunctionDelegate GetTokenHashFunction(
        SecretKey secretKey,
        out int hashSizeBytes
    )
    {
        var okpSecretKey = secretKey.Validate<OkpSecretKey>(KeyBitSizes);

        if (!TryResolveCurve(okpSecretKey, out var resolved))
        {
            throw new InvalidOperationException(
                $"The OKP curve '{okpSecretKey.Curve}' is not supported by the '{Code}' algorithm."
            );
        }

        return OkpCurves.GetTokenHashFunction(resolved, out hashSizeBytes);
    }

    /// <summary>
    /// Gets the <see cref="HashAlgorithmName"/> used for derived hashes (for example OpenID Connect <c>at_hash</c> /
    /// <c>c_hash</c>). <c>EdDSA</c> hashes internally; SHA-512 is the conventional companion hash.
    /// </summary>
    public override HashAlgorithmName HashAlgorithmName => HashAlgorithmName.SHA512;

    /// <inheritdoc />
    public override int GetSignatureSizeBytes(int keySizeBits) =>
        OkpCurves.TryGetByKeySizeBits(keySizeBits, out var curve)
            ? curve.SignatureSize
            : throw new ArgumentOutOfRangeException(
                nameof(keySizeBits),
                keySizeBits,
                "The key size does not correspond to a supported OKP curve."
            );

    /// <inheritdoc />
    public override bool TrySign(
        SecretKey secretKey,
        ReadOnlySpan<byte> inputData,
        Span<byte> signature,
        out int bytesWritten
    )
    {
        var okpSecretKey = secretKey.Validate<OkpSecretKey>(KeyBitSizes);

        if (!TryResolveCurve(okpSecretKey, out var curve))
        {
            throw new InvalidOperationException(
                $"The OKP curve '{okpSecretKey.Curve}' is not supported by the '{Code}' algorithm."
            );
        }

        if (!okpSecretKey.HasPrivateKey)
        {
            throw new InvalidOperationException(
                "The OKP secret key holds only the public half and cannot sign."
            );
        }

        if (signature.Length < curve.SignatureSize)
        {
            bytesWritten = 0;
            return false;
        }

        Span<byte> seed = stackalloc byte[curve.SeedSize];
        try
        {
            var writer = new FixedSpanBufferWriter<byte>(seed);
            okpSecretKey.ExportPrivateKey(ref writer);
            OkpCurves.Sign(curve.Name, seed, inputData, signature[..curve.SignatureSize]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seed);
        }

        bytesWritten = curve.SignatureSize;
        return true;
    }

    /// <inheritdoc />
    public override bool Verify(
        SecretKey secretKey,
        ReadOnlySpan<byte> inputData,
        ReadOnlySpan<byte> signature
    )
    {
        var okpSecretKey = secretKey.Validate<OkpSecretKey>(KeyBitSizes);

        if (!TryResolveCurve(okpSecretKey, out var curve))
        {
            throw new InvalidOperationException(
                $"The OKP curve '{okpSecretKey.Curve}' is not supported by the '{Code}' algorithm."
            );
        }

        if (signature.Length != curve.SignatureSize)
        {
            return false;
        }

        Span<byte> publicKey = stackalloc byte[curve.PublicKeySize];
        var writer = new FixedSpanBufferWriter<byte>(publicKey);
        okpSecretKey.ExportPublicKey(ref writer);

        return OkpCurves.Verify(curve.Name, publicKey, inputData, signature);
    }
}
