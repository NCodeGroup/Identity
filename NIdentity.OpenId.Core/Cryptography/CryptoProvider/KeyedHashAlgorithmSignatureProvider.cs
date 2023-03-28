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

using NIdentity.OpenId.Cryptography.Descriptors;

namespace NIdentity.OpenId.Cryptography.CryptoProvider;

/// <summary>
/// Provides an implementation of <see cref="SignatureProvider"/> that uses a keyed hash digital signature algorithm.
/// </summary>
public class KeyedHashAlgorithmSignatureProvider : SignatureProvider
{
    private SharedSecretKey SharedSecretKey { get; }
    private KeyedHashAlgorithmDescriptor Descriptor { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyedHashAlgorithmSignatureProvider"/> class.
    /// </summary>
    /// <param name="secretKey">Contains the key material used by the keyed hash algorithm.</param>
    /// <param name="descriptor">Contains the <see cref="KeyedHashAlgorithmDescriptor"/> that describes the keyed hash algorithm.</param>
    public KeyedHashAlgorithmSignatureProvider(SharedSecretKey secretKey, KeyedHashAlgorithmDescriptor descriptor)
        : base(secretKey, descriptor)
    {
        SharedSecretKey = secretKey;
        Descriptor = descriptor;
    }

    /// <inheritdoc />
    public override bool TrySign(ReadOnlySpan<byte> input, Span<byte> signature, out int bytesWritten)
    {
        var hashByteLength = AlgorithmDescriptor.HashBitLength;
        if (signature.Length < hashByteLength)
        {
            bytesWritten = 0;
            return false;
        }

        /*
           A key of the same size as the hash output (for instance, 256 bits for
           "HS256") or larger MUST be used with this algorithm.  (This
           requirement is based on Section 5.3.4 (Security Effect of the HMAC
           Key) of NIST SP 800-117 [NIST.800-107], which states that the
           effective security strength is the minimum of the security strength
           of the key and two times the size of the internal hash value.)
        */

        // TODO: can this be validated earlier?
        var keyByteLength = SharedSecretKey.GetKeyByteLength();
        if (keyByteLength < hashByteLength)
        {
            throw new InvalidOperationException();
        }

        var key = keyByteLength <= BinaryUtility.StackAllocMax ?
            stackalloc byte[keyByteLength] :
            new byte[keyByteLength];

        var keyBytesWritten = SharedSecretKey.GetKeyBytes(key);
        if (keyBytesWritten != keyByteLength)
        {
            throw new InvalidOperationException();
        }

        return Descriptor.KeyedHashFunction(key, input, signature, out bytesWritten);
    }
}
