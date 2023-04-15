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
using NIdentity.OpenId.Cryptography.Keys;

namespace NIdentity.OpenId.Cryptography.CryptoProvider.AesGcm;

/// <summary>
/// Provides an implementation of <see cref="AuthenticatedEncryptionProvider"/> using the <c>AES GCM</c> algorithm.
/// </summary>
public class AesGcmAuthenticatedEncryptionProvider : AuthenticatedEncryptionProvider
{
    /// <summary>
    /// Gets the <see cref="SymmetricSecretKey"/> containing the key material used by the <c>AES GCM</c> algorithm.
    /// </summary>
    public SymmetricSecretKey SymmetricSecretKey { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="AesGcmAuthenticatedEncryptionProvider"/> class.
    /// </summary>
    /// <param name="secretKey">Contains key material for the <c>AES GCM</c> algorithm.</param>
    /// <param name="descriptor">Contains an <see cref="AuthenticatedEncryptionAlgorithmDescriptor"/> that describes the <c>AES GCM</c> algorithm.</param>
    public AesGcmAuthenticatedEncryptionProvider(SymmetricSecretKey secretKey, AuthenticatedEncryptionAlgorithmDescriptor descriptor)
        : base(secretKey, descriptor)
    {
        SymmetricSecretKey = secretKey;
    }

    private System.Security.Cryptography.AesGcm CreateAesGcm() =>
        new(SymmetricSecretKey.KeyBytes);

    /// <inheritdoc />
    public override void Encrypt(
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plainText,
        ReadOnlySpan<byte> associatedData,
        Span<byte> cipherText,
        Span<byte> authenticationTag)
    {
        if (plainText.Length != cipherText.Length)
            throw new InvalidOperationException();

        using var aesGcm = CreateAesGcm();
        aesGcm.Encrypt(nonce, plainText, cipherText, authenticationTag, associatedData);
    }

    /// <inheritdoc />
    public override bool TryDecrypt(
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> cipherText,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> authenticationTag,
        Span<byte> plainText,
        out int bytesWritten)
    {
        if (plainText.Length != cipherText.Length)
        {
            bytesWritten = 0;
            return false;
        }

        using var aesGcm = CreateAesGcm();
        aesGcm.Decrypt(nonce, cipherText, authenticationTag, plainText, associatedData);

        bytesWritten = cipherText.Length;
        return true;
    }
}
