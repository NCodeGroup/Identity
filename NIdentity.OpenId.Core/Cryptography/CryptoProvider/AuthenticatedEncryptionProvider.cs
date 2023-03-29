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
/// Base implementation for all cryptographic authenticated encryption <c>AEAD</c> algorithms.
/// </summary>
public abstract class AuthenticatedEncryptionProvider : IDisposable
{
    /// <summary>
    /// Gets the <see cref="SecretKey"/> containing the key material used by the cryptographic <c>AEAD</c> algorithm.
    /// </summary>
    public SecretKey SecretKey { get; }

    /// <summary>
    /// Gets an <see cref="AuthenticatedEncryptionAlgorithmDescriptor"/> that describes the cryptographic <c>AEAD</c> algorithm.
    /// </summary>
    public AuthenticatedEncryptionAlgorithmDescriptor AlgorithmDescriptor { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticatedEncryptionProvider"/> class.
    /// </summary>
    /// <param name="secretKey">Contains the key material used by the cryptographic <c>AEAD</c> algorithm.</param>
    /// <param name="descriptor">Contains an <see cref="AuthenticatedEncryptionAlgorithmDescriptor"/> that describes the cryptographic <c>AEAD</c> algorithm.</param>
    protected AuthenticatedEncryptionProvider(SecretKey secretKey, AuthenticatedEncryptionAlgorithmDescriptor descriptor)
    {
        SecretKey = secretKey;
        AlgorithmDescriptor = descriptor;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// When overridden in a derived class, releases the unmanaged resources used by the
    /// <see cref="AuthenticatedEncryptionProvider"/>, and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c>
    /// to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        // nothing
    }

    /// <summary>
    /// When overridden in a derived class, encrypts the plaintext into the ciphertext destination buffer
    /// and generates the authentication tag into a separate buffer.
    /// </summary>
    /// <param name="nonce">The nonce associated with this message, which should be a unique value for every operation with the same key.</param>
    /// <param name="plainText">The content to encrypt.</param>
    /// <param name="associatedData">Extra data associated with this message, which must also be provided during decryption.</param>
    /// <param name="cipherText">The byte array to receive the encrypted contents.</param>
    /// <param name="authenticationTag">The byte array to receive the generated authentication tag.</param>
    public abstract void Encrypt(
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plainText,
        ReadOnlySpan<byte> associatedData,
        Span<byte> cipherText,
        Span<byte> authenticationTag);

    /// <summary>
    /// When overridden in a derived class, decrypts the ciphertext into the provided destination buffer if the authentication tag can be validated.
    /// </summary>
    /// <param name="nonce">The nonce associated with this message, which must match the value provided during encryption.</param>
    /// <param name="cipherText">The encrypted content to decrypt.</param>
    /// <param name="associatedData">Extra data associated with this message, which must match the value provided during encryption.</param>
    /// <param name="authenticationTag">The authentication tag produced for this message during encryption.</param>
    /// <param name="plainText">The byte array to receive the decrypted contents.</param>
    /// <param name="bytesWritten">The number of bytes written to <paramref name="plainText"/>.</param>
    /// <returns><c>true</c> if <paramref name="plainText"/> was large enough to receive the decrypted data; otherwise, <c>false</c>.</returns>
    public abstract bool TryDecrypt(
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> cipherText,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> authenticationTag,
        Span<byte> plainText,
        out int bytesWritten);
}
