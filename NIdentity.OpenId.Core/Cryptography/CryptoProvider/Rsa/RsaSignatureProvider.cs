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

using NIdentity.OpenId.Cryptography.CryptoProvider.Rsa.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.Signature;
using NIdentity.OpenId.Cryptography.Keys;

namespace NIdentity.OpenId.Cryptography.CryptoProvider.Rsa;

internal class RsaSignatureProvider : SignatureProvider
{
    private RsaSignatureAlgorithmDescriptor RsaSignatureAlgorithmDescriptor { get; }

    private RsaSecretKey RsaSecretKey { get; }

    public RsaSignatureProvider(RsaSecretKey secretKey, RsaSignatureAlgorithmDescriptor descriptor)
        : base(secretKey, descriptor)
    {
        RsaSignatureAlgorithmDescriptor = descriptor;
        RsaSecretKey = secretKey;
    }

    /// <inheritdoc />
    public override bool TrySign(ReadOnlySpan<byte> input, Span<byte> signature, out int bytesWritten)
    {
        using var rsa = RsaSecretKey.CreateRSA();
        return rsa.TrySignData(
            input,
            signature,
            RsaSignatureAlgorithmDescriptor.HashAlgorithmName,
            RsaSignatureAlgorithmDescriptor.Padding,
            out bytesWritten);
    }

    /// <inheritdoc />
    public override bool Verify(ReadOnlySpan<byte> input, ReadOnlySpan<byte> signature)
    {
        using var rsa = RsaSecretKey.CreateRSA();
        return rsa.VerifyData(
            input,
            signature,
            RsaSignatureAlgorithmDescriptor.HashAlgorithmName,
            RsaSignatureAlgorithmDescriptor.Padding);
    }
}
