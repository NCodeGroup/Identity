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

using NIdentity.OpenId.Cryptography.CryptoProvider.Aead;
using NIdentity.OpenId.Cryptography.CryptoProvider.Aead.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.Signature;
using NIdentity.OpenId.Cryptography.CryptoProvider.Signature.Descriptors;

namespace NIdentity.OpenId.Cryptography.Keys;

public abstract class SecretKey : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        // nothing
    }

    public virtual SignatureProvider CreateSignatureProvider(SignatureAlgorithmDescriptor descriptor) =>
        descriptor.CryptoFactory.CreateSignatureProvider(this, descriptor);

    public virtual KeyWrapProvider CreateKeyWrapProvider(KeyWrapAlgorithmDescriptor descriptor) =>
        descriptor.CryptoFactory.CreateKeyWrapProvider(this, descriptor);

    public virtual AuthenticatedEncryptionProvider CreateAuthenticatedEncryptionProvider(AuthenticatedEncryptionAlgorithmDescriptor descriptor) =>
        descriptor.CryptoFactory.CreateAuthenticatedEncryptionProvider(this, descriptor);
}
