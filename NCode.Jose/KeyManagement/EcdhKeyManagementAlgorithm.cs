#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using NCode.Cryptography.Keys;
using NCode.Encoders;
using NCode.Jose.Exceptions;
using NCode.Jose.Extensions;

namespace NCode.Jose.KeyManagement;

/// <summary>
/// Provides an implementation of <see cref="KeyManagementAlgorithm"/> that uses the <c>ECDH-ES</c> cryptographic algorithm for key management.
/// </summary>
public class EcdhKeyManagementAlgorithm : KeyManagementAlgorithm
{
    private static IEnumerable<KeySizes> StaticKekBitSizes { get; } = new KeySizes[]
    {
        new(minSize: 256, maxSize: 384, skipSize: 128),
        new(minSize: 521, maxSize: 521, skipSize: 0)
    };

    private static IEnumerable<KeySizes> StaticCekByteSizes { get; } = new[]
    {
        new KeySizes(minSize: 1, maxSize: int.MaxValue, skipSize: 1)
    };

    /// <inheritdoc />
    public override string Code { get; }

    /// <inheritdoc />
    public override Type KeyType => typeof(EccSecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KeyBitSizes => StaticKekBitSizes;

    private string AlgorithmField { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EcdhKeyManagementAlgorithm"/> class
    /// for usage in key agreement with key wrapping mode.
    /// </summary>
    public EcdhKeyManagementAlgorithm()
        : this(AlgorithmCodes.KeyManagement.EcdhEs, isDirectAgreement: true)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EcdhKeyManagementAlgorithm"/> class
    /// for usage in key agreement with key wrapping mode.
    /// </summary>
    /// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
    /// <param name="isDirectAgreement"><c>true</c> if the algorithm is to use direct key agreement mode;
    /// otherwise, <c>false</c> if the algorithm is to use key agreement with key wrapping mode.</param>
    protected EcdhKeyManagementAlgorithm(string code, bool isDirectAgreement)
    {
        Code = code;
        AlgorithmField = isDirectAgreement ? "enc" : "alg";
    }

    internal static unsafe void ExportKey(int curveSizeBits, ECDiffieHellman key, IDictionary<string, object> headers)
    {
        var parameters = key.ExportParameters(includePrivateParameters: true);

        // Hmm. What's the point when the header has this value unprotected?
        // ReSharper disable once UnusedVariable
        fixed (byte* pinned = parameters.D)
        {
            try
            {
                headers["epk"] = new Dictionary<string, object>
                {
                    ["kty"] = "EC",
                    ["crv"] = $"P-{curveSizeBits}",
                    ["x"] = Base64Url.Encode(parameters.Q.X),
                    ["y"] = Base64Url.Encode(parameters.Q.Y),
                    ["d"] = Base64Url.Encode(parameters.D)
                };
            }
            finally
            {
                CryptographicOperations.ZeroMemory(parameters.D);
            }
        }
    }

    internal ECDiffieHellman ValidateHeaderForUnwrap(
        ECCurve curve,
        int curveSizeBits,
        IReadOnlyDictionary<string, object> header,
        out string algorithm,
        out string? apu,
        out string? apv)
    {
        if (!header.TryGetValue<string>(AlgorithmField, out var localAlgorithm))
        {
            throw new JoseException($"The JWT header is missing the '{AlgorithmField}' field.");
        }

        if (!header.TryGetValue<IDictionary<string, object>>("epk", out var epk))
        {
            throw new JoseException("The JWT header is missing the 'epk' field.");
        }

        if (!epk.TryGetValue<string>("kty", out var kty))
        {
            throw new JoseException("The 'epk' header is missing the 'kty' field.");
        }

        if (!string.Equals(kty, "EC", StringComparison.Ordinal))
        {
            throw new JoseException("The 'kty' field was expected to be 'EC'.");
        }

        if (!epk.TryGetValue<string>("crv", out var crv))
        {
            throw new JoseException("The 'epk' header is missing the 'crv' field.");
        }

        var crvExpected = $"P-{curveSizeBits}";
        if (!string.Equals(crv, crvExpected, StringComparison.Ordinal))
        {
            throw new JoseException($"The 'crv' field was expected to be '{crvExpected}'.");
        }

        if (!epk.TryGetValue<string>("x", out var x))
        {
            throw new JoseException("The 'epk' header is missing the 'x' field.");
        }

        if (!epk.TryGetValue<string>("y", out var y))
        {
            throw new JoseException("The 'epk' header is missing the 'y' field.");
        }

        var parameters = new ECParameters
        {
            Curve = curve,
            Q = new ECPoint
            {
                X = Base64Url.Decode(x),
                Y = Base64Url.Decode(y)
            }
        };

        algorithm = localAlgorithm;

        header.TryGetValue("apu", out apu);
        header.TryGetValue("apv", out apv);

        return ECDiffieHellman.Create(parameters);
    }

    /// <inheritdoc />
    public override IEnumerable<KeySizes> GetLegalCekByteSizes(int kekSizeBits) => StaticCekByteSizes;

    /// <inheritdoc />
    public override int GetEncryptedContentKeySizeBytes(int kekSizeBits, int cekSizeBytes) => 0;

    /// <inheritdoc />
    public override void NewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey)
    {
        var validatedSecretKey = ValidateSecretKey<EccSecretKey>(secretKey);

        var curve = validatedSecretKey.GetECCurve();
        var curveSizeBits = validatedSecretKey.KeySizeBits;

        using var recipientKey = validatedSecretKey.ExportECDiffieHellman();
        using var ephemeralKey = ECDiffieHellman.Create(curve);
        ExportKey(curveSizeBits, ephemeralKey, header);

        if (!header.TryGetValue<string>(AlgorithmField, out var algorithm))
        {
            throw new JoseException($"The JWT header is missing the '{AlgorithmField}' field.");
        }

        header.TryGetValue<string>("apu", out var apu);
        header.TryGetValue<string>("apv", out var apv);

        using var senderKey = ephemeralKey.PublicKey;
        DeriveKey(algorithm, apu, apv, curveSizeBits, recipientKey, senderKey, contentKey);
    }

    /// <inheritdoc />
    public override bool TryWrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        Span<byte> encryptedContentKey,
        out int bytesWritten)
    {
        throw new JoseException("The 'ECDH-ES' key management algorithm does not support using an existing CEK.");
    }

    /// <inheritdoc />
    public override bool TryUnwrapKey(
        SecretKey secretKey,
        IReadOnlyDictionary<string, object> header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten)
    {
        if (encryptedContentKey.Length != 0)
        {
            throw new ArgumentException(
                "The encrypted content encryption key (CEK) does not have a valid size for this cryptographic algorithm.",
                nameof(encryptedContentKey));
        }

        var validatedSecretKey = ValidateSecretKey<EccSecretKey>(secretKey);

        var curve = validatedSecretKey.GetECCurve();
        var curveSizeBits = validatedSecretKey.KeySizeBits;

        using var recipientKey = validatedSecretKey.ExportECDiffieHellman();
        using var ephemeralKey = ValidateHeaderForUnwrap(curve, curveSizeBits, header, out var algorithm, out var apu, out var apv);
        using var senderKey = ephemeralKey.PublicKey;

        DeriveKey(algorithm, apu, apv, curveSizeBits, recipientKey, senderKey, contentKey);

        bytesWritten = contentKey.Length;
        return true;
    }

    private static void DeriveKey(string algorithm,
        string? apu,
        string? apv,
        int curveSizeBits,
        ECDiffieHellman recipientKey,
        ECDiffieHellmanPublicKey senderKey,
        Span<byte> destination)
    {
        var keySizeBytes = destination.Length;
        var keySizeBits = keySizeBytes << 3;

        int hashSizeBits;
        HashAlgorithmName hashAlgorithmName;

        switch (curveSizeBits)
        {
            case <= 256:
                hashSizeBits = 256;
                hashAlgorithmName = HashAlgorithmName.SHA256;
                break;

            case <= 384:
                hashSizeBits = 384;
                hashAlgorithmName = HashAlgorithmName.SHA384;
                break;

            default:
                hashSizeBits = 512;
                hashAlgorithmName = HashAlgorithmName.SHA512;
                break;
        }

        // can't use span/stackalloc because DeriveKeyFromHash doesn't
        var secretPrependBytes = GC.AllocateUninitializedArray<byte>(sizeof(int));
        var secretAppendBytes = GetSecretAppendBytes(algorithm, apu, apv, keySizeBits);

        var reps = (keySizeBits + hashSizeBits - 1) / hashSizeBits;
        for (var rep = 1; rep <= reps; ++rep)
        {
            BinaryPrimitives.WriteInt32BigEndian(secretPrependBytes.AsSpan(), rep);

            // too bad this API doesn't support Span<T> :(
            var partialKey = recipientKey.DeriveKeyFromHash(
                senderKey,
                hashAlgorithmName,
                secretPrependBytes,
                secretAppendBytes);

            var partialLength = Math.Min(partialKey.Length, destination.Length);
            var partialSpan = partialKey.AsSpan(0, partialLength);

            partialSpan.CopyTo(destination);
            destination = destination[partialLength..];
        }
    }

    private static byte[] GetSecretAppendBytes(
        string algorithm,
        string? apu,
        string? apv,
        int keySizeBits)
    {
        var algorithmByteCount = Encoding.ASCII.GetByteCount(algorithm);
        var apuByteCount = string.IsNullOrEmpty(apu) ? 0 : Base64Url.GetByteCountForDecode(apu.Length);
        var apvByteCount = string.IsNullOrEmpty(apv) ? 0 : Base64Url.GetByteCountForDecode(apv.Length);

        var secretAppendByteCount =
            // algorithm length prefix
            sizeof(int) +
            // algorithm ASCII bytes
            algorithmByteCount +
            // apu length prefix
            sizeof(int) +
            // apu bytes
            apuByteCount +
            // apv length prefix
            sizeof(int) +
            // apv bytes
            apvByteCount +
            // key size bits
            sizeof(int);

        var bytes = GC.AllocateUninitializedArray<byte>(secretAppendByteCount);
        var pos = bytes.AsSpan();

        // algorithm length prefix
        BinaryPrimitives.WriteInt32BigEndian(pos, algorithmByteCount);
        pos = pos[sizeof(int)..];

        // algorithm ASCII bytes
        var bytesWritten = Encoding.ASCII.GetBytes(algorithm, pos);
        Debug.Assert(bytesWritten == algorithmByteCount);
        pos = pos[algorithmByteCount..];

        // apu length prefix
        BinaryPrimitives.WriteInt32BigEndian(pos, apuByteCount);
        pos = pos[sizeof(int)..];

        // apu bytes
        var result = Base64Url.TryDecode(apu, pos, out bytesWritten);
        Debug.Assert(result && bytesWritten == apuByteCount);
        pos = pos[apuByteCount..];

        // apv length prefix
        BinaryPrimitives.WriteInt32BigEndian(pos, apvByteCount);
        pos = pos[sizeof(int)..];

        // apv bytes
        result = Base64Url.TryDecode(apv, pos, out bytesWritten);
        Debug.Assert(result && bytesWritten == apvByteCount);
        pos = pos[apvByteCount..];

        // key size bits
        BinaryPrimitives.WriteInt32BigEndian(pos, keySizeBits);

        return bytes;
    }
}
