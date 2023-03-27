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
/// Provides factory methods to create providers for various cryptographic algorithms.
/// </summary>
public interface ICryptoFactory
{
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
    /// Creates an instance of <see cref="KeyWrapProvider"/> that can be used for authenticated encryption algorithms.
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
/// <typeparam name="T">The concrete <see cref="ICryptoFactory"/> type.</typeparam>
public abstract class CryptoFactory<T> : CryptoFactory
    where T : CryptoFactory<T>, new()
{
    /// <summary>
    /// Provides a default singleton instance for <see cref="T"/>.
    /// </summary>
    public static ICryptoFactory Default { get; } = new T();
}
