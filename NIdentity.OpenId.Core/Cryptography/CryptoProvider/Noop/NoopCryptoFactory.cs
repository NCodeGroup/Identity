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

using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.Signature;
using NIdentity.OpenId.Cryptography.CryptoProvider.Signature.Descriptors;
using NIdentity.OpenId.Cryptography.Descriptors;
using NIdentity.OpenId.Cryptography.Keys;

namespace NIdentity.OpenId.Cryptography.CryptoProvider.Noop;

/// <summary>
/// Provides factory methods to create providers that do nothing for certain cryptographic operations.
/// </summary>
public class NoopCryptoFactory : CryptoFactory<NoopCryptoFactory, SecretKey>
{
    /// <inheritdoc />
    protected override SecretKey CoreGenerateNewKey(AlgorithmDescriptor descriptor, int? keyBitLengthHint = default) =>
        throw new InvalidOperationException();

    /// <inheritdoc />
    public override SignatureProvider CreateSignatureProvider(
        SecretKey secretKey,
        SignatureAlgorithmDescriptor descriptor) =>
        new NoopSignatureProvider(secretKey, descriptor);

    /// <inheritdoc />
    public override KeyWrapProvider CreateKeyWrapProvider(
        SecretKey secretKey,
        KeyWrapAlgorithmDescriptor descriptor) =>
        new NoopKeyWrapProvider(secretKey, descriptor);
}