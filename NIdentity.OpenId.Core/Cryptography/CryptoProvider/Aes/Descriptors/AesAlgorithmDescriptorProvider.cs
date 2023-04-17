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

namespace NIdentity.OpenId.Cryptography.CryptoProvider.Aes.Descriptors;

/// <summary>
/// Provides an implementation of <see cref="IAlgorithmDescriptorProvider"/> that returns a collection of
/// descriptors for the <c>AES Key Wrap</c> algorithm.
/// </summary>
public class AesAlgorithmDescriptorProvider : IAlgorithmDescriptorProvider
{
    /// <inheritdoc />
    public IEnumerable<AlgorithmDescriptor> Load() => new[]
    {
        new AesKeyWrapAlgorithmDescriptor(
            AlgorithmCodes.KeyManagement.Aes128,
            KeySizeBits: 128),

        new AesKeyWrapAlgorithmDescriptor(
            AlgorithmCodes.KeyManagement.Aes192,
            KeySizeBits: 192),

        new AesKeyWrapAlgorithmDescriptor(
            AlgorithmCodes.KeyManagement.Aes256,
            KeySizeBits: 256),
    };
}
