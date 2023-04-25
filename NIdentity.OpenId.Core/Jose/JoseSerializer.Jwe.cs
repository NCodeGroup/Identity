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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NIdentity.OpenId.Buffers;
using NIdentity.OpenId.Cryptography.CryptoProvider.Ecdh.Parameters;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap.Parameters;
using NIdentity.OpenId.Cryptography.Keys;
using NIdentity.OpenId.Logic;

namespace NIdentity.OpenId.Jose;

public class JweToken
{
}

partial class JoseSerializer
{
    private JweToken ParseJwe(string jwe, ISecretKeyCollection secretKeys)
    {
        var trimmed = jwe.Trim();
        var isJson = trimmed.StartsWith('{') && trimmed.EndsWith('}');
        return isJson ? ParseJweJson(trimmed, secretKeys) : ParseJweCompact(trimmed, secretKeys);
    }

    private JweToken ParseJweCompact(string jwe, ISecretKeyCollection secretKeys)
    {
        /*
              BASE64URL(UTF8(JWE Protected Header)) || '.' ||
              BASE64URL(JWE Encrypted Key) || '.' ||
              BASE64URL(JWE Initialization Vector) || '.' ||
              BASE64URL(JWE Ciphertext) || '.' ||
              BASE64URL(JWE Authentication Tag)
        */

        // JWE Protected Header
        var segment = StringSplitSequenceSegment.Split(jwe, '.', out var count);
        if (count != JweSegmentCount) throw new ArgumentException("The input is not a valid JWE value in compact form.", nameof(jwe));
        var encodedHeader = segment.Memory.Span;
        var headerBytes = Base64Url.Decode(encodedHeader);
        var deserializedHeader = Deserialize<IReadOnlyDictionary<string, object>>(headerBytes, "JWE Protected Header");

        // JWE Encrypted Key
        segment = segment.Next ?? throw new InvalidOperationException();
        var encryptedKey = segment.Memory.Span;

        // JWE Initialization Vector
        segment = segment.Next ?? throw new InvalidOperationException();
        var initializationVector = segment.Memory.Span;

        // JWE Ciphertext
        segment = segment.Next ?? throw new InvalidOperationException();
        var cipherText = segment.Memory.Span;

        // JWE Authentication Tag
        segment = segment.Next ?? throw new InvalidOperationException();
        var authenticationTag = segment.Memory.Span;

        Debug.Assert(segment.Next == null);

        if (!TryGetValue<string>(deserializedHeader, "alg", out var keyWrapAlgorithmCode))
        {
            throw new JoseException("The JWT header is missing the 'alg' field.");
        }

        if (!TryGetValue<string>(deserializedHeader, "enc", out var encryptionAlgorithmCode))
        {
            throw new JoseException("The JWT header is missing the 'enc' field.");
        }

        if (!TryGetValue<string>(deserializedHeader, "kid", out var keyId))
        {
            throw new JoseException("The JWT header is missing the 'kid' field.");
        }

        if (!AlgorithmCollection.TryGetKeyWrapAlgorithm(keyWrapAlgorithmCode, out var keyWrapAlgorithm))
        {
            throw new InvalidAlgorithmException($"No registered JWA key agreement algorithm for `{keyWrapAlgorithmCode}` was found.");
        }

        if (!AlgorithmCollection.TryGetAuthenticatedEncryptionAlgorithm(encryptionAlgorithmCode, out var encryptionAlgorithm))
        {
            throw new InvalidAlgorithmException($"No registered AEAD encryption algorithm for `{encryptionAlgorithmCode}` was found.");
        }

        if (!secretKeys.TryGet(keyId, out var secretKey))
        {
            throw new JoseException($"No registered key for `{keyId}' was found.");
        }

        using var keyWrapProvider = secretKey.CreateKeyWrapProvider(keyWrapAlgorithm);

        //var keyUnwrapParameters = new KeyUnwrapParameters();
        //var cek = keyWrapProvider.UnwrapKey(keyUnwrapParameters);

        throw new NotImplementedException();
    }

    private KeyUnwrapParameters CreateEcdhEsKeyUnwrapWithAesKeyUnwrapParameters(
        ReadOnlyMemory<byte> encryptedKey,
        KeyWrapAlgorithmDescriptor descriptor,
        IReadOnlyDictionary<string, object> header)
    {
        if (!TryGetValue<IReadOnlyDictionary<string, object>>(header, "epk", out var epk))
        {
            throw new JoseException("The JWT header is missing the 'epk' field.");
        }

        TryGetValue<string?>(header, "apu", out var apu);
        TryGetValue<string?>(header, "apv", out var apv);

        if (!TryGetValue<string>(epk, "x", out var x))
        {
            throw new JoseException("The 'epk' header is missing the 'x' field.");
        }

        if (!TryGetValue<string>(epk, "y", out var y))
        {
            throw new JoseException("The 'epk' header is missing the 'y' field.");
        }

        if (!TryGetValue<string>(epk, "crv", out var crv))
        {
            throw new JoseException("The 'epk' header is missing the 'crv' field.");
        }

        var ecParameters = new ECParameters
        {
            Curve = ECCurveFromHeaderValue(crv, out var keySizeBits),
            Q = new ECPoint
            {
                X = Base64Url.Decode(x),
                Y = Base64Url.Decode(y)
            }
        };

        return new EcdhEsKeyUnwrapWithAesKeyUnwrapParameters(
            encryptedKey,
            ecParameters,
            keySizeBits,
            apu,
            apv);
    }

    private static ECCurve ECCurveFromHeaderValue(string value, out int keySizeBits)
    {
        switch (value)
        {
            case "P-256":
                keySizeBits = 256;
                return ECCurve.NamedCurves.nistP256;

            case "P-384":
                keySizeBits = 384;
                return ECCurve.NamedCurves.nistP384;

            case "P-521":
                keySizeBits = 521;
                return ECCurve.NamedCurves.nistP521;

            default:
                throw new JoseException("An unsupported value for 'crv' was specified.");
        }
    }

    public JweToken ParseJweJson(string jwe, ISecretKeyCollection secretKeys)
    {
        throw new NotImplementedException();
    }
}

internal interface IKeyWrapParameterFactory<in TState>
{
    KeyWrapParameters CreateKeyWrapParameters(
        ReadOnlyMemory<byte> plainTextKey,
        KeyWrapAlgorithmDescriptor descriptor,
        TState state);

    KeyUnwrapParameters CreateKeyUnwrapParameters(
        ReadOnlyMemory<byte> cipherTextKey,
        KeyWrapAlgorithmDescriptor descriptor,
        TState state);
}

// CreateEcdhEsKeyUnwrapWithAesKeyUnwrapParameters

internal class JoseKeyWrapParameterFactoryState
{
}

internal class JoseEcdhEsKeyWrapWithAesKeyWrapParameterFactory : IKeyWrapParameterFactory<IReadOnlyDictionary<string, object>>
{
    public KeyWrapParameters CreateKeyWrapParameters(
        ReadOnlyMemory<byte> plainTextKey,
        KeyWrapAlgorithmDescriptor descriptor,
        IReadOnlyDictionary<string, object> state)
    {
        throw new NotImplementedException();
    }

    public KeyUnwrapParameters CreateKeyUnwrapParameters(
        ReadOnlyMemory<byte> cipherTextKey,
        KeyWrapAlgorithmDescriptor descriptor,
        IReadOnlyDictionary<string, object> state)
    {
        throw new NotImplementedException();
    }
}
