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
using NCode.Identity.Jose.Algorithms;

namespace NCode.Identity.Jose.Tests.Extensions;

public class HashAlgorithmNameExtensionsTests
{
    public static IEnumerable<object?[]> GetHashSizeBitsTestData()
    {
        yield return [HashAlgorithmName.MD5, null];
        yield return [HashAlgorithmName.SHA1, 160];
        yield return [HashAlgorithmName.SHA256, 256];
        yield return [HashAlgorithmName.SHA384, 384];
        yield return [HashAlgorithmName.SHA512, 512];
    }

    [Theory]
    [MemberData(nameof(GetHashSizeBitsTestData))]
    public void GetHashSizeBits_WithVariousAlgorithms_ReturnsOrThrows(
        HashAlgorithmName hashAlgorithmName,
        int? expected
    )
    {
        if (expected.HasValue)
        {
            var actual = hashAlgorithmName.GetHashSizeBits();
            Assert.Equal(expected.Value, actual);
        }
        else
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                hashAlgorithmName.GetHashSizeBits()
            );

            Assert.Equal(
                $"The {hashAlgorithmName} hash algorithm is not supported. (Parameter 'hashAlgorithmName')",
                exception.Message
            );
        }
    }

    [Fact]
    public void GetHashSizeBits_WithSha3WhenThePlatformSupportsIt_ReturnsItsSize()
    {
        // A no-op where the running OS provides no SHA-3, as the ML-DSA integration tests are.
        if (!SHA3_256.IsSupported)
            return;

        Assert.Equal(256, HashAlgorithmName.SHA3_256.GetHashSizeBits());
    }

    [Fact]
    public void GetHashSizeBits_WithAnUnknownAlgorithm_Throws()
    {
        var hashAlgorithmName = new HashAlgorithmName("NOT-A-HASH");

        var exception = Assert.Throws<ArgumentException>(() => hashAlgorithmName.GetHashSizeBits());

        Assert.Equal(
            $"The {hashAlgorithmName} hash algorithm is not supported. (Parameter 'hashAlgorithmName')",
            exception.Message
        );
    }

    public static IEnumerable<object[]> GetHashFunctionTestData()
    {
        yield return [HashAlgorithmName.SHA1];
        yield return [HashAlgorithmName.SHA256];
        yield return [HashAlgorithmName.SHA384];
        yield return [HashAlgorithmName.SHA512];
    }

    [Theory]
    [MemberData(nameof(GetHashFunctionTestData))]
    public void GetHashFunction_WithASupportedAlgorithm_HashesLikeTheFramework(
        HashAlgorithmName hashAlgorithmName
    )
    {
        var source = "hash me"u8.ToArray();
        var destination = new byte[hashAlgorithmName.GetHashSizeBits() / 8];

        var hashed = hashAlgorithmName.GetHashFunction()(source, destination, out var bytesWritten);

        Assert.True(hashed);
        Assert.Equal(destination.Length, bytesWritten);
        Assert.Equal(CryptographicOperations.HashData(hashAlgorithmName, source), destination);
    }

    [Fact]
    public void GetHashFunction_WithSha3WhenThePlatformSupportsIt_HashesLikeTheFramework()
    {
        // A no-op where the running OS provides no SHA-3, as the ML-DSA integration tests are.
        if (!SHA3_256.IsSupported)
            return;

        var source = "hash me"u8.ToArray();
        var destination = new byte[32];

        var hashed = HashAlgorithmName.SHA3_256.GetHashFunction()(
            source,
            destination,
            out var bytesWritten
        );

        Assert.True(hashed);
        Assert.Equal(32, bytesWritten);
        Assert.Equal(SHA3_256.HashData(source), destination);
    }

    [Fact]
    public void GetHashFunction_WithMd5_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            HashAlgorithmName.MD5.GetHashFunction()
        );

        Assert.Equal(
            $"The {HashAlgorithmName.MD5} hash algorithm is not supported. (Parameter 'hashAlgorithmName')",
            exception.Message
        );
    }
}
