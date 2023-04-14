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

using System.Collections;
using System.Security.Cryptography;
using NIdentity.OpenId.Cryptography.CryptoProvider.Pbes2.Descriptors;

namespace NIdentity.OpenId.Core.Tests.Cryptography.Pbes2;

public class Pbes2RoundTripTestData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        yield return new object[]
        {
            "secret_password",
            new Pbes2KeyWrapAlgorithmDescriptor(
                AlgorithmCodes.KeyManagement.Pbes2HmacSha256Aes128,
                HashAlgorithmName.SHA256,
                HashSizeBits: 256,
                KeySizeBits: 128,
                DefaultIterationCount: 310000)
        };

        yield return new object[]
        {
            "other_password",
            new Pbes2KeyWrapAlgorithmDescriptor(
                AlgorithmCodes.KeyManagement.Pbes2HmacSha384Aes192,
                HashAlgorithmName.SHA384,
                HashSizeBits: 384,
                KeySizeBits: 192,
                DefaultIterationCount: 250000)
        };

        yield return new object[]
        {
            Guid.NewGuid().ToString("N"),
            new Pbes2KeyWrapAlgorithmDescriptor(
                AlgorithmCodes.KeyManagement.Pbes2HmacSha512Aes256,
                HashAlgorithmName.SHA512,
                HashSizeBits: 512,
                KeySizeBits: 256,
                DefaultIterationCount: 120000)
        };
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
