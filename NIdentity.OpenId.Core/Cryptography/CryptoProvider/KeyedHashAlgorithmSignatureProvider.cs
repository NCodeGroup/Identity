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

internal class KeyedHashAlgorithmSignatureProvider : SignatureProvider
{
    private SharedSecretKey SharedSecretKey { get; }
    private KeyedHashAlgorithmDescriptor Descriptor { get; }

    public KeyedHashAlgorithmSignatureProvider(SharedSecretKey secretKey, KeyedHashAlgorithmDescriptor descriptor)
        : base(secretKey, descriptor)
    {
        SharedSecretKey = secretKey;
        Descriptor = descriptor;
    }

    public override bool TrySign(ReadOnlySpan<byte> input, Span<byte> signature, out int bytesWritten)
    {
        if (signature.Length < HashByteLength)
        {
            bytesWritten = 0;
            return false;
        }

        var key = SharedSecretKey.GetKeyBytes();
        using var keyedHashAlgorithm = Descriptor.KeyedHashAlgorithmFactory(key);
        return keyedHashAlgorithm.TryComputeHash(input, signature, out bytesWritten);
    }
}
