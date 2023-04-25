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

    internal virtual void DeriveKey(
        string partyUInfo,
        string partyVInfo,
        ECDiffieHellman recipientKey,
        ECDiffieHellmanPublicKey senderKey,
        Span<byte> derivedKey)
    {
        throw new NotImplementedException();
    }

    internal virtual ReadOnlySequence<byte> DeriveKey(IEcdhEsAgreement agreement, ECDiffieHellman recipientKey, ECDiffieHellmanPublicKey senderKey)
    {
        var keyByteLength = agreement.KeySizeBits / 8;
        var hashByteLength = EcdhDescriptor.HashSizeBits / 8;
        var reps = (keyByteLength + hashByteLength - 1) / hashByteLength;

        // can't use span/stackalloc because DeriveKeyFromHash doesn't
        var secretPrependBytes = GC.AllocateUninitializedArray<byte>(sizeof(int));

        using var secretAppendStream = new MemoryStream();
        using var secretAppendWriter = new KdfBinaryWriter(secretAppendStream);
        secretAppendWriter.Write(EcdhDescriptor.AlgorithmCode);
        secretAppendWriter.WriteBase64Url(agreement.PartyUInfo);
        secretAppendWriter.WriteBase64Url(agreement.PartyVInfo);
        secretAppendWriter.Write(agreement.KeySizeBits);
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
            var partialKey = recipientKey.DeriveKeyFromHash(
                senderKey,
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

    protected T ValidateParameters<T>(WrapNewKeyParameters parameters)
        where T : EccWrapNewKeyParameters
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

    public SecretKey WrapNewKey(WrapNewKeyParameters parameters, Span<byte> derivedKey)
    {
        var typedParameters = ValidateParameters<EccWrapNewKeyParameters>(parameters);

        var ephemeralKeyId = Guid.NewGuid().ToString("N");
        var ephemeralKey = (EccSecretKey)EcdhDescriptor.CryptoFactory.GenerateNewKey(ephemeralKeyId, AlgorithmDescriptor);
        try
        {
            using var recipientKey = ephemeralKey.CreateECDiffieHellman();
            using var ourPrivateKey = EccSecretKey.CreateECDiffieHellman();
            using var ourPublicKey = ourPrivateKey.PublicKey;

            DeriveKey(
                typedParameters.PartyUInfo,
                typedParameters.PartyVInfo,
                recipientKey,
                ourPublicKey,
                derivedKey);

            return ephemeralKey;
        }
        catch
        {
            ephemeralKey.Dispose();
            throw;
        }
    }

    public void WrapKey(WrapKeyParameters parameters, ReadOnlySpan<byte> cek, Span<byte> derivedKey)
    {
        throw new NotSupportedException("The ECDH-ES key management algorithm does not support using an existing CEK.");
    }

    public void UnwrapKey(UnwrapKeyParameters parameters, ReadOnlySpan<byte> derivedKey, Span<byte> cek)
    {
        throw new NotImplementedException();
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
        using var senderKey = ECDiffieHellman.Create(typedParameters.SenderPublicKey);
        using var senderPublicKey = senderKey.PublicKey;

        return DeriveKey(typedParameters, ourPrivateKey, senderPublicKey);
    }
}
