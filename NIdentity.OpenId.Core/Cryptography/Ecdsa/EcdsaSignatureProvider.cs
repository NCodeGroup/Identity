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

using NIdentity.OpenId.Cryptography.CryptoProvider;
using NIdentity.OpenId.Cryptography.Descriptors;
using NIdentity.OpenId.Cryptography.Ecc;

namespace NIdentity.OpenId.Cryptography.Ecdsa;

internal class EcdsaSignatureProvider : SignatureProvider
{
    private EccSecretKey EccSecretKey { get; }
    private HashSignatureAlgorithmDescriptor Descriptor { get; }

    public EcdsaSignatureProvider(EccSecretKey secretKey, HashSignatureAlgorithmDescriptor descriptor)
        : base(secretKey, descriptor)
    {
        EccSecretKey = secretKey;
        Descriptor = descriptor;
    }

    public override bool TrySign(ReadOnlySpan<byte> input, Span<byte> signature, out int bytesWritten)
    {
        if (signature.Length < Descriptor.HashByteLength)
        {
            bytesWritten = 0;
            return false;
        }

        using var ecdsa = EccSecretKey.CreateECDsa();

        return ecdsa.TrySignData(input, signature, Descriptor.HashAlgorithmName, out bytesWritten);
    }

    public override bool Verify(ReadOnlySpan<byte> input, ReadOnlySpan<byte> signature)
    {
        if (signature.Length != Descriptor.HashByteLength)
            return false;

        using var ecdsa = EccSecretKey.CreateECDsa();

        return ecdsa.VerifyData(input, signature, Descriptor.HashAlgorithmName);
    }
}
