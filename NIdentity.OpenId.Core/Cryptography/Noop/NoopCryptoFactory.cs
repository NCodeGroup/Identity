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

namespace NIdentity.OpenId.Cryptography.Noop;

internal class NoopCryptoFactory : ICryptoFactory
{
    public static ICryptoFactory Default { get; } = new NoopCryptoFactory();

    /// <inheritdoc />
    public SignatureProvider CreateSignatureProvider(SecretKey secretKey, SignatureAlgorithmDescriptor descriptor)
    {
        return new NoopSignatureProvider(secretKey, descriptor);
    }

    /// <inheritdoc />
    public KeyWrapProvider CreateKeyWrapProvider(SecretKey secretKey, KeyWrapAlgorithmDescriptor descriptor)
    {
        return new NoopKeyWrapProvider(secretKey, descriptor);
    }

    /// <inheritdoc />
    public AuthenticatedEncryptionProvider CreateAuthenticatedEncryptionProvider(SecretKey secretKey, AlgorithmDescriptor descriptor)
    {
        throw new InvalidOperationException();
    }
}
