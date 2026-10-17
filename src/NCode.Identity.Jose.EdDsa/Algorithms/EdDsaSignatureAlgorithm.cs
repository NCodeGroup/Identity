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

using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.EdDsa.Keys;
using NCode.Identity.Jose.EdDsa.Logic;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;

namespace NCode.Identity.Jose.EdDsa.Algorithms;

/// <summary>
/// The <c>EdDSA</c> (RFC 8037) <see cref="SignatureAlgorithm"/>. A single <c>JOSE</c> algorithm code (<c>EdDSA</c>)
/// covers both Edwards curves; the concrete curve (Ed25519 or Ed448) is selected from the key's
/// <see cref="OkpSecretKey.Curve"/> at signing and verification time.
/// </summary>
internal sealed class EdDsaSignatureAlgorithm : SignatureAlgorithm
{
    /// <inheritdoc />
    public override string Code => AlgorithmCodes.DigitalSignature.EdDsa;

    /// <inheritdoc />
    public override Type KeyType => typeof(OkpSecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KeyBitSizes { get; } =
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

        if (!OkpCurves.TryGetByName(okpSecretKey.Curve, out var curve))
        {
            throw new InvalidOperationException(
                $"The OKP curve '{okpSecretKey.Curve}' is not supported."
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

        if (!OkpCurves.TryGetByName(okpSecretKey.Curve, out var curve))
        {
            throw new InvalidOperationException(
                $"The OKP curve '{okpSecretKey.Curve}' is not supported."
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
