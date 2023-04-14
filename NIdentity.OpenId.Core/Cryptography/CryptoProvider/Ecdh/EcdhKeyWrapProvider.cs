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

using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using NIdentity.OpenId.Cryptography.Binary;
using NIdentity.OpenId.Cryptography.CryptoProvider.Ecdh.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.Ecdh.Parameters;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap.Parameters;
using NIdentity.OpenId.Cryptography.Keys;

namespace NIdentity.OpenId.Cryptography.CryptoProvider.Ecdh;

// Used by:
// https://datatracker.ietf.org/doc/html/rfc7518#section-4.6
// ECDH-ES
// ECDH-ES+A128KW
// ECDH-ES+A192KW
// ECDH-ES+A256KW

// KDF Algorithm:
// http://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-56Ar2.pdf
// Concat KDF, as defined in Section 5.8.1 of [NIST.800-56A]
// reps = ceil( keydatalen / hashlen )
// K(i) = H(counter || Z || OtherInfo)
// DerivedKeyingMaterial = K(1) || K(2) || … || K(reps-1) || K_Last

// H: SHA265
// Z: the ECDH shared secret
// keydatalen: 128, 192, or 256
// AlgorithmID: value from `enc` or `alg` header
// PartyUInfo: value from `apu` header
// PartyVInfo: value from `apv` header
// SuppPubInfo: keydatalen
// SuppPrivInfo: empty

// Example implementation:
// https://stackoverflow.com/a/10971402/2502089

internal class EcdhKeyWrapProvider : KeyWrapProvider
{
    protected EccSecretKey EccSecretKey { get; }

    protected EcdhKeyWrapAlgorithmDescriptor EcdhDescriptor { get; }

    public EcdhKeyWrapProvider(EccSecretKey secretKey, EcdhKeyWrapAlgorithmDescriptor descriptor)
        : base(secretKey, descriptor)
    {
        EccSecretKey = secretKey;
        EcdhDescriptor = descriptor;
    }

    internal virtual ReadOnlySequence<byte> DeriveKey(IEcdhEsAgreement agreement, ECDiffieHellman privateKeyParty1, ECDiffieHellmanPublicKey publicKeyParty2)
    {
        var keyByteLength = agreement.KeyBitLength / 8;
        var hashByteLength = EcdhDescriptor.HashSizeBits / 8;
        var reps = (keyByteLength + hashByteLength - 1) / hashByteLength;

        // can't use span/stackalloc because DeriveKeyFromHash doesn't
        var secretPrependBytes = GC.AllocateUninitializedArray<byte>(sizeof(int));

        using var secretAppendStream = new MemoryStream();
        using var secretAppendWriter = new KdfBinaryWriter(secretAppendStream);
        secretAppendWriter.Write(EcdhDescriptor.AlgorithmCode);
        secretAppendWriter.Write(agreement.PartyUInfo);
        secretAppendWriter.Write(agreement.PartyVInfo);
        secretAppendWriter.Write(agreement.KeyBitLength);
        var secretAppendBytes = secretAppendStream.ToArray();

        var keyBuffer = reps == 1 ?
            Array.Empty<byte>() :
            GC.AllocateUninitializedArray<byte>(keyByteLength);

        ReadOnlySequence<byte> TrimKey(ReadOnlyMemory<byte> key) =>
            new(hashByteLength > keyByteLength ? key[..keyByteLength] : key);

        for (var counter = 1; counter <= reps; ++counter)
        {
            BinaryPrimitives.WriteInt32BigEndian(secretPrependBytes.AsSpan(), counter);

            // too bad this API doesn't support Span<T> :(
            var partialKey = privateKeyParty1.DeriveKeyFromHash(
                publicKeyParty2,
                EcdhDescriptor.HashAlgorithmName,
                secretPrependBytes,
                secretAppendBytes);

            if (reps == 1)
            {
                return TrimKey(partialKey);
            }

            var offset = (counter - 1) * hashByteLength;
            var remainder = keyByteLength % hashByteLength;
            var count = remainder == 0 ? hashByteLength : remainder;
            Buffer.BlockCopy(partialKey, 0, keyBuffer, offset, count);
        }

        return TrimKey(keyBuffer);
    }

    protected T ValidateParameters<T>(KeyWrapParameters parameters)
        where T : EcdhEsKeyWrapParameters
    {
        if (parameters is not T typedParameters)
        {
            throw new InvalidOperationException();
        }

        if (!string.Equals(EcdhDescriptor.KeyDerivationFunction, KeyDerivationFunctionTypes.SP800_56A_CONCAT, StringComparison.Ordinal))
        {
            throw new InvalidOperationException();
        }

        return typedParameters;
    }

    /// <inheritdoc />
    public override ReadOnlySequence<byte> WrapKey(KeyWrapParameters parameters)
    {
        var typedParameters = ValidateParameters<EcdhEsKeyWrapParameters>(parameters);

        using var ourPrivateKey = EccSecretKey.CreateECDiffieHellman();
        using var ourPublicKey = ourPrivateKey.PublicKey;

        return DeriveKey(typedParameters, typedParameters.RecipientKey, ourPublicKey);
    }

    protected T ValidateParameters<T>(KeyUnwrapParameters parameters)
        where T : EcdhEsKeyUnwrapParameters
    {
        if (parameters is not T typedParameters)
        {
            throw new InvalidOperationException();
        }

        if (!string.Equals(EcdhDescriptor.KeyDerivationFunction, KeyDerivationFunctionTypes.SP800_56A_CONCAT, StringComparison.Ordinal))
        {
            throw new InvalidOperationException();
        }

        return typedParameters;
    }

    /// <inheritdoc />
    public override ReadOnlySequence<byte> UnwrapKey(KeyUnwrapParameters parameters)
    {
        var typedParameters = ValidateParameters<EcdhEsKeyUnwrapParameters>(parameters);

        using var ourPrivateKey = EccSecretKey.CreateECDiffieHellman();

        return DeriveKey(typedParameters, ourPrivateKey, typedParameters.SenderPublicKey);
    }
}
