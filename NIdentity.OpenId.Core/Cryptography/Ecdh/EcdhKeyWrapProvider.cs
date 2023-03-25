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
using System.Security.Cryptography;
using NIdentity.OpenId.Cryptography.CryptoProvider;

namespace NIdentity.OpenId.Cryptography.Ecdh;

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

public class EcdhKeyWrapProvider : KeyWrapProvider
{
    public EcdhSecretKey EcdhSecretKey { get; }

    private EcdhKeyWrapAlgorithmDescriptor EcdhDescriptor { get; }

    public EcdhKeyWrapProvider(EcdhSecretKey secretKey, EcdhKeyWrapAlgorithmDescriptor descriptor)
        : base(secretKey, descriptor)
    {
        EcdhSecretKey = secretKey;
        EcdhDescriptor = descriptor;
    }

    internal virtual ReadOnlyMemory<byte> DeriveKey(IEcdhEsAgreement agreement, ECDiffieHellman privateKeyParty1, ECDiffieHellmanPublicKey publicKeyParty2)
    {
        var keyByteLength = agreement.KeyBitLength / 8;
        var hashByteLength = EcdhDescriptor.HashBitLength / 8;
        var reps = (keyByteLength + hashByteLength - 1) / hashByteLength;

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

        ReadOnlyMemory<byte> TrimKey(ReadOnlyMemory<byte> key) =>
            hashByteLength > keyByteLength ? key[..keyByteLength] : key;

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

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> WrapKey(KeyWrapParameters parameters)
    {
        if (parameters is not EcdhEsKeyWrapParameters typedParameters)
        {
            throw new InvalidOperationException();
        }

        if (!string.Equals(EcdhDescriptor.KeyDerivationFunction, KeyDerivationFunctionTypes.SP800_56A_CONCAT, StringComparison.Ordinal))
        {
            throw new InvalidOperationException();
        }

        using var ourPublicKey = EcdhSecretKey.Key.PublicKey;
        return DeriveKey(typedParameters, typedParameters.RecipientKey, ourPublicKey);
    }

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> UnwrapKey(KeyUnwrapParameters parameters)
    {
        if (parameters is not EcdhEsKeyUnwrapParameters typedParameters)
        {
            throw new InvalidOperationException();
        }

        if (!string.Equals(EcdhDescriptor.KeyDerivationFunction, KeyDerivationFunctionTypes.SP800_56A_CONCAT, StringComparison.Ordinal))
        {
            throw new InvalidOperationException();
        }

        return DeriveKey(typedParameters, EcdhSecretKey.Key, typedParameters.SenderPublicKey);
    }
}
