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

using System.Security.Cryptography;
using NCode.Jose.Extensions;

namespace NCode.Jose.Tests.Extensions;

public class HashAlgorithmNameExtensionsTests
{
    public static IEnumerable<object?[]> GetHashSizeBitsTestData()
    {
        yield return new object?[] { HashAlgorithmName.MD5, null };
        yield return new object?[] { HashAlgorithmName.SHA1, 160 };
        yield return new object?[] { HashAlgorithmName.SHA256, 256 };
        yield return new object?[] { HashAlgorithmName.SHA384, 384 };
        yield return new object?[] { HashAlgorithmName.SHA512, 512 };
    }

    [Theory]
    [MemberData(nameof(GetHashSizeBitsTestData))]
    public void GetHashSizeBits_Valid(HashAlgorithmName hashAlgorithmName, int? expected)
    {
        if (expected.HasValue)
        {
            var actual = hashAlgorithmName.GetHashSizeBits();
            Assert.Equal(expected.Value, actual);
        }
        else
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                hashAlgorithmName.GetHashSizeBits());

            Assert.Equal($"The {hashAlgorithmName} hash algorithm is not supported. (Parameter 'hashAlgorithmName')", exception.Message);
        }
    }
}
