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

using NIdentity.OpenId.Cryptography.Binary;
using NIdentity.OpenId.Cryptography.CryptoProvider.Aead;
using NIdentity.OpenId.Cryptography.CryptoProvider.Aead.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.Signature;
using NIdentity.OpenId.Cryptography.CryptoProvider.Signature.Descriptors;
using NIdentity.OpenId.Cryptography.Descriptors;
using NIdentity.OpenId.Cryptography.Keys;
using NIdentity.OpenId.Cryptography.Keys.Material;

namespace NIdentity.OpenId.Cryptography.CryptoProvider;

/// <summary>
/// Provides factory methods to create providers for various cryptographic algorithms.
/// </summary>
public interface ICryptoFactory
{
    /// <summary>
    /// Gets the <see cref="Type"/> of the <see cref="SecretKey"/> used by this <see cref="ICryptoFactory"/>.
    /// </summary>
    Type SecretKeyType { get; }

    /// <summary>
    /// Generates a new <see cref="SecretKey"/> for the specified algorithm <paramref name="descriptor"/> with a key size hint.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <param name="descriptor">The <see cref="AlgorithmDescriptor"/> that describes the cryptographic algorithm.</param>
    /// <param name="keySizeBitsHint">An optional value that specifies the key size in bits to generate.
    /// This value is verified against the legal key sizes for the algorithm.
    /// If omitted, the first legal key size is used.</param>
    /// <returns>The newly generated <see cref="SecretKey"/>.</returns>
    SecretKey GenerateNewKey(string keyId, AlgorithmDescriptor descriptor, int? keySizeBitsHint = default);

    /// <summary>
    /// Creates an instance of <see cref="SignatureProvider"/> that can be used for digital signature algorithms.
    /// </summary>
    /// <param name="secretKey">The key material for the cryptographic algorithm.</param>
    /// <param name="descriptor">The <see cref="SignatureAlgorithmDescriptor"/> that describes the cryptographic algorithm.</param>
    /// <returns>A new instance of <see cref="SignatureProvider"/>.</returns>
    SignatureProvider CreateSignatureProvider(
        SecretKey secretKey,
        SignatureAlgorithmDescriptor descriptor);

    /// <summary>
    /// Creates an instance of <see cref="KeyWrapProvider"/> that can be used for key management algorithms.
    /// </summary>
    /// <param name="secretKey">The key material for the cryptographic algorithm.</param>
    /// <param name="descriptor">The <see cref="KeyWrapAlgorithmDescriptor"/> that describes the cryptographic algorithm.</param>
    /// <returns>A new instance of <see cref="KeyWrapProvider"/>.</returns>
    KeyWrapProvider CreateKeyWrapProvider(
        SecretKey secretKey,
        KeyWrapAlgorithmDescriptor descriptor);

    /// <summary>
    /// Creates an instance of <see cref="AuthenticatedEncryptionProvider"/> that can be used for authenticated encryption (i.e. AEAD) algorithms.
    /// </summary>
    /// <param name="secretKey">The key material for the cryptographic algorithm.</param>
    /// <param name="descriptor">The <see cref="AuthenticatedEncryptionAlgorithmDescriptor"/> that describes the cryptographic algorithm.</param>
    /// <returns>A new instance of <see cref="AuthenticatedEncryptionProvider"/>.</returns>
    AuthenticatedEncryptionProvider CreateAuthenticatedEncryptionProvider(
        SecretKey secretKey,
        AuthenticatedEncryptionAlgorithmDescriptor descriptor);
}

/// <summary>
/// Provides a default implementation of <see cref="ICryptoFactory"/> where all the methods throw unless overriden.
/// </summary>
public abstract class CryptoFactory : ICryptoFactory
{
    /// <inheritdoc />
    public abstract Type SecretKeyType { get; }

    /// <inheritdoc />
    public virtual SecretKey GenerateNewKey(string keyId, AlgorithmDescriptor descriptor, int? keySizeBitsHint = default)
    {
        var keySizeBits = KeySizesUtility.GetLegalSize(descriptor, keySizeBitsHint);

        using var keyMaterial = GenerateKeyMaterial(keySizeBits);

        return SecretKeyFactory.Create(keyMaterial, keyBytes => CreateSecretKey(keyId, keySizeBits, keyBytes));
    }

    /// <summary>
    /// Generates new cryptographic random key material.
    /// </summary>
    /// <param name="keySizeBits">The length of the key material in bits.</param>
    /// <returns>A <see cref="KeyMaterial"/> that contains the cryptographic random key material.</returns>
    protected abstract KeyMaterial GenerateKeyMaterial(int keySizeBits);

    /// <summary>
    /// Factory method to create a new <see cref="SecretKey"/> with the specified key material.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <param name="keySizeBits">The length of the key material in bits.</param>
    /// <param name="keyBytes">The key material for the secret key.</param>
    /// <returns>The newly created <see cref="SecretKey"/>.</returns>
    protected abstract SecretKey CreateSecretKey(string keyId, int keySizeBits, ReadOnlySpan<byte> keyBytes);

    /// <summary>
    /// Validates that the specified <paramref name="secretKey"/> is an instance of <typeparamref name="T"/>.
    /// </summary>
    /// <param name="secretKey">The <see cref="SecretKey"/> to validate.</param>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <returns><paramref name="secretKey"/> casted to <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="secretKey"/> is not a type of <typeparamref name="T"/>.</exception>
    protected static T ValidateSecretKey<T>(SecretKey secretKey)
        where T : SecretKey
    {
        if (secretKey is not T typedSecretKey)
        {
            throw new ArgumentException($"The security key was expected to be of type {typeof(T).FullName}", nameof(secretKey));
        }

        return typedSecretKey;
    }

    /// <summary>
    /// Validates that the specified <paramref name="descriptor"/> is an instance of <typeparamref name="T"/>.
    /// </summary>
    /// <param name="descriptor">The <see cref="AlgorithmDescriptor"/> to validate.</param>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <returns><paramref name="descriptor"/> casted to <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="descriptor"/> is not a type of <typeparamref name="T"/>.</exception>
    protected static T ValidateDescriptor<T>(AlgorithmDescriptor descriptor)
        where T : AlgorithmDescriptor
    {
        if (descriptor is not T typedDescriptor)
        {
            throw new ArgumentException($"The descriptor was expected to be of type {typeof(T).FullName}", nameof(descriptor));
        }

        return typedDescriptor;
    }

    /// <inheritdoc />
    public virtual SignatureProvider CreateSignatureProvider(
        SecretKey secretKey,
        SignatureAlgorithmDescriptor descriptor) =>
        throw new InvalidOperationException("The current CryptoFactory instance does not support creating signature providers.");

    /// <inheritdoc />
    public virtual KeyWrapProvider CreateKeyWrapProvider(
        SecretKey secretKey,
        KeyWrapAlgorithmDescriptor descriptor) =>
        throw new InvalidOperationException("The current CryptoFactory instance does not support creating key wrap providers.");

    /// <inheritdoc />
    public virtual AuthenticatedEncryptionProvider CreateAuthenticatedEncryptionProvider(
        SecretKey secretKey,
        AuthenticatedEncryptionAlgorithmDescriptor descriptor) =>
        throw new InvalidOperationException("The current CryptoFactory instance does not support creating authenticated encryption providers.");
}

/// <summary>
/// Provides a default implementation of <see cref="ICryptoFactory"/> where all the methods throw unless overriden.
/// </summary>
/// <typeparam name="TCryptoFactory">The concrete <see cref="ICryptoFactory"/> type.</typeparam>
public abstract class CryptoFactory<TCryptoFactory> : CryptoFactory
    where TCryptoFactory : CryptoFactory<TCryptoFactory>, new()
{
    /// <summary>
    /// Provides a default singleton instance for <typeparamref name="TCryptoFactory"/>.
    /// </summary>
    public static ICryptoFactory Default { get; } = new TCryptoFactory();
}
