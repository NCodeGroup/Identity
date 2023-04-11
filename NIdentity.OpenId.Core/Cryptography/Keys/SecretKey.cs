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

/// <summary>
/// Base class for all cryptographic key material.
/// </summary>
public abstract class SecretKey : IDisposable
{
    /// <summary>
    /// Gets the <c>Key ID (KID)</c> of this <see cref="SecretKey"/>.
    /// </summary>
    public string KeyId { get; }

    /// <summary>
    /// Gets the length of the key material in bits.
    /// </summary>
    public abstract int KeySizeBits { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretKey"/> class with the specified <c>Key ID (KID)</c>.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    protected SecretKey(string keyId)
    {
        KeyId = keyId;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// When overridden in a derived class, releases the unmanaged resources used by the
    /// <see cref="SecretKey"/>, and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c>
    /// to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        // nothing
    }

    /// <summary>
    /// Creates an instance of <see cref="SignatureProvider"/> that can be used for digital signature algorithms.
    /// </summary>
    /// <param name="descriptor">The <see cref="SignatureAlgorithmDescriptor"/> that describes the cryptographic algorithm.</param>
    /// <returns>A new instance of <see cref="SignatureProvider"/>.</returns>
    public virtual SignatureProvider CreateSignatureProvider(SignatureAlgorithmDescriptor descriptor) =>
        descriptor.CryptoFactory.CreateSignatureProvider(this, descriptor);

    /// <summary>
    /// Creates an instance of <see cref="KeyWrapProvider"/> that can be used for key management algorithms.
    /// </summary>
    /// <param name="descriptor">The <see cref="KeyWrapAlgorithmDescriptor"/> that describes the cryptographic algorithm.</param>
    /// <returns>A new instance of <see cref="KeyWrapProvider"/>.</returns>
    public virtual KeyWrapProvider CreateKeyWrapProvider(KeyWrapAlgorithmDescriptor descriptor) =>
        descriptor.CryptoFactory.CreateKeyWrapProvider(this, descriptor);

    /// <summary>
    /// Creates an instance of <see cref="AuthenticatedEncryptionProvider"/> that can be used for authenticated encryption (i.e. AEAD) algorithms.
    /// </summary>
    /// <param name="descriptor">The <see cref="AuthenticatedEncryptionAlgorithmDescriptor"/> that describes the cryptographic algorithm.</param>
    /// <returns>A new instance of <see cref="AuthenticatedEncryptionProvider"/>.</returns>
    public virtual AuthenticatedEncryptionProvider CreateAuthenticatedEncryptionProvider(AuthenticatedEncryptionAlgorithmDescriptor descriptor) =>
        descriptor.CryptoFactory.CreateAuthenticatedEncryptionProvider(this, descriptor);
}
