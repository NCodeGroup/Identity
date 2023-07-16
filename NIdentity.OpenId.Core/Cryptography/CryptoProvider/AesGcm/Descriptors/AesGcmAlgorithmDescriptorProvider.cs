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

namespace NIdentity.OpenId.Cryptography.CryptoProvider.AesGcm.Descriptors;

/// <summary>
/// Provides an implementation of <see cref="IAlgorithmDescriptorProvider"/> that returns a collection of
/// descriptors for the <c>AES GCM</c> algorithm.
/// </summary>
public class AesGcmAlgorithmDescriptorProvider : IAlgorithmDescriptorProvider
{
    /// <inheritdoc />
    public IEnumerable<AlgorithmDescriptor> Load() => new[]
    {
        new AesGcmAuthenticatedEncryptionAlgorithmDescriptor(
            AlgorithmCodes.AuthenticatedEncryption.Aes128Gcm,
            KeyBitLength: 128),

        new AesGcmAuthenticatedEncryptionAlgorithmDescriptor(
            AlgorithmCodes.AuthenticatedEncryption.Aes192Gcm,
            KeyBitLength: 192),

        new AesGcmAuthenticatedEncryptionAlgorithmDescriptor(
            AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm,
            KeyBitLength: 256),
    };
}
