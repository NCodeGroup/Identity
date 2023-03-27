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

namespace NIdentity.OpenId.Cryptography.AesGcm;

internal class AesGcmAuthenticatedEncryptionProvider : AuthenticatedEncryptionProvider
{
    private SharedSecretKey SharedSecretKey { get; }
    private AesGcmAuthenticatedEncryptionAlgorithmDescriptor Descriptor { get; }

    public AesGcmAuthenticatedEncryptionProvider(SharedSecretKey secretKey, AesGcmAuthenticatedEncryptionAlgorithmDescriptor descriptor)
        : base(secretKey, descriptor)
    {
        SharedSecretKey = secretKey;
        Descriptor = descriptor;
    }

    private System.Security.Cryptography.AesGcm CreateAesGcm()
    {
        var keyByteLength = Descriptor.KeyByteLength;
        var key = keyByteLength <= BinaryUtility.StackAllocMax ?
            stackalloc byte[keyByteLength] :
            new byte[keyByteLength];

        var bytesWritten = SharedSecretKey.GetKeyBytes(key);
        if (bytesWritten != keyByteLength)
        {
            throw new InvalidOperationException();
        }

        return new System.Security.Cryptography.AesGcm(key);
    }

    public override void Encrypt(
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plainText,
        ReadOnlySpan<byte> associatedData,
        Span<byte> cipherText,
        Span<byte> authenticationTag)
    {
        using var aesGcm = CreateAesGcm();
        aesGcm.Encrypt(nonce, plainText, cipherText, authenticationTag, associatedData);
    }

    public override void Decrypt(
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> cipherText,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> authenticationTag,
        Span<byte> plainText)
    {
        using var aesGcm = CreateAesGcm();
        aesGcm.Decrypt(nonce, cipherText, authenticationTag, plainText, associatedData);
    }
}
