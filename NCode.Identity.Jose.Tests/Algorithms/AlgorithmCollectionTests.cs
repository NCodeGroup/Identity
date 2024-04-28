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

using NCode.Identity.Jose.Algorithms;

namespace NCode.Jose.Tests.Algorithms;

public class AlgorithmCollectionTests : BaseTests
{
    [Fact]
    public void Algorithms_Valid()
    {
        var mockAlgorithm1 = CreateStrictMock<Algorithm>();
        var mockAlgorithm2 = CreateStrictMock<Algorithm>();
        var mockAlgorithm3 = CreateStrictMock<Algorithm>();

        mockAlgorithm1
            .Setup(x => x.Code)
            .Returns("code1")
            .Verifiable();
        mockAlgorithm2
            .Setup(x => x.Code)
            .Returns("code2")
            .Verifiable();
        mockAlgorithm3
            .Setup(x => x.Code)
            .Returns("code3")
            .Verifiable();

        mockAlgorithm1
            .Setup(x => x.Type)
            .Returns(AlgorithmType.Unspecified)
            .Verifiable();
        mockAlgorithm2
            .Setup(x => x.Type)
            .Returns(AlgorithmType.Unspecified)
            .Verifiable();
        mockAlgorithm3
            .Setup(x => x.Type)
            .Returns(AlgorithmType.Unspecified)
            .Verifiable();

        var algorithms = new[]
        {
            mockAlgorithm1.Object,
            mockAlgorithm2.Object,
            mockAlgorithm3.Object
        };

        Assert.Contains(mockAlgorithm1.Object, algorithms);
        Assert.Contains(mockAlgorithm2.Object, algorithms);
        Assert.Contains(mockAlgorithm3.Object, algorithms);
    }

    [Fact]
    public void TryGetSignatureAlgorithm_Valid()
    {
        var mockAlgorithm1 = CreateStrictMock<Algorithm>();
        var mockAlgorithm2 = CreateStrictMock<SignatureAlgorithm>();
        var mockAlgorithm3 = CreateStrictMock<Algorithm>();

        mockAlgorithm1
            .Setup(x => x.Code)
            .Returns("code1")
            .Verifiable();
        mockAlgorithm2
            .Setup(x => x.Code)
            .Returns("code2")
            .Verifiable();
        mockAlgorithm3
            .Setup(x => x.Code)
            .Returns("code3")
            .Verifiable();

        mockAlgorithm1
            .Setup(x => x.Type)
            .Returns(AlgorithmType.Unspecified)
            .Verifiable();
        mockAlgorithm2
            .Setup(x => x.Type)
            .Returns(AlgorithmType.DigitalSignature)
            .Verifiable();
        mockAlgorithm3
            .Setup(x => x.Type)
            .Returns(AlgorithmType.Unspecified)
            .Verifiable();

        var algorithms = new AlgorithmCollection(new[]
        {
            mockAlgorithm1.Object,
            mockAlgorithm2.Object,
            mockAlgorithm3.Object
        });
        Assert.Equal(3, algorithms.Count);

        var result1 = algorithms.TryGetSignatureAlgorithm("code1", out var algorithm1);
        Assert.False(result1);
        Assert.Null(algorithm1);

        var result2 = algorithms.TryGetSignatureAlgorithm("code2", out var algorithm2);
        Assert.True(result2);
        Assert.NotNull(algorithm2);

        var result3 = algorithms.TryGetSignatureAlgorithm("code3", out var algorithm3);
        Assert.False(result3);
        Assert.Null(algorithm3);
    }

    [Fact]
    public void TryGetKeyManagementAlgorithm_Valid()
    {
        var mockAlgorithm1 = CreateStrictMock<Algorithm>();
        var mockAlgorithm2 = CreateStrictMock<KeyManagementAlgorithm>();
        var mockAlgorithm3 = CreateStrictMock<Algorithm>();

        mockAlgorithm1
            .Setup(x => x.Code)
            .Returns("code1")
            .Verifiable();
        mockAlgorithm2
            .Setup(x => x.Code)
            .Returns("code2")
            .Verifiable();
        mockAlgorithm3
            .Setup(x => x.Code)
            .Returns("code3")
            .Verifiable();

        mockAlgorithm1
            .Setup(x => x.Type)
            .Returns(AlgorithmType.Unspecified)
            .Verifiable();
        mockAlgorithm2
            .Setup(x => x.Type)
            .Returns(AlgorithmType.KeyManagement)
            .Verifiable();
        mockAlgorithm3
            .Setup(x => x.Type)
            .Returns(AlgorithmType.Unspecified)
            .Verifiable();

        var algorithms = new AlgorithmCollection(new[]
        {
            mockAlgorithm1.Object,
            mockAlgorithm2.Object,
            mockAlgorithm3.Object
        });
        Assert.Equal(3, algorithms.Count);

        var result1 = algorithms.TryGetKeyManagementAlgorithm("code1", out var algorithm1);
        Assert.False(result1);
        Assert.Null(algorithm1);

        var result2 = algorithms.TryGetKeyManagementAlgorithm("code2", out var algorithm2);
        Assert.True(result2);
        Assert.NotNull(algorithm2);

        var result3 = algorithms.TryGetKeyManagementAlgorithm("code3", out var algorithm3);
        Assert.False(result3);
        Assert.Null(algorithm3);
    }

    [Fact]
    public void TryGetAuthenticatedEncryptionAlgorithm_Valid()
    {
        var mockAlgorithm1 = CreateStrictMock<Algorithm>();
        var mockAlgorithm2 = CreateStrictMock<AuthenticatedEncryptionAlgorithm>();
        var mockAlgorithm3 = CreateStrictMock<Algorithm>();

        mockAlgorithm1
            .Setup(x => x.Code)
            .Returns("code1")
            .Verifiable();
        mockAlgorithm2
            .Setup(x => x.Code)
            .Returns("code2")
            .Verifiable();
        mockAlgorithm3
            .Setup(x => x.Code)
            .Returns("code3")
            .Verifiable();

        mockAlgorithm1
            .Setup(x => x.Type)
            .Returns(AlgorithmType.Unspecified)
            .Verifiable();
        mockAlgorithm2
            .Setup(x => x.Type)
            .Returns(AlgorithmType.AuthenticatedEncryption)
            .Verifiable();
        mockAlgorithm3
            .Setup(x => x.Type)
            .Returns(AlgorithmType.Unspecified)
            .Verifiable();

        var algorithms = new AlgorithmCollection(new[]
        {
            mockAlgorithm1.Object,
            mockAlgorithm2.Object,
            mockAlgorithm3.Object
        });
        Assert.Equal(3, algorithms.Count);

        var result1 = algorithms.TryGetAuthenticatedEncryptionAlgorithm("code1", out var algorithm1);
        Assert.False(result1);
        Assert.Null(algorithm1);

        var result2 = algorithms.TryGetAuthenticatedEncryptionAlgorithm("code2", out var algorithm2);
        Assert.True(result2);
        Assert.NotNull(algorithm2);

        var result3 = algorithms.TryGetAuthenticatedEncryptionAlgorithm("code3", out var algorithm3);
        Assert.False(result3);
        Assert.Null(algorithm3);
    }

    [Fact]
    public void TryGetCompressionAlgorithm_Valid()
    {
        var mockAlgorithm1 = CreateStrictMock<Algorithm>();
        var mockAlgorithm2 = CreateStrictMock<CompressionAlgorithm>();
        var mockAlgorithm3 = CreateStrictMock<Algorithm>();

        mockAlgorithm1
            .Setup(x => x.Code)
            .Returns("code1")
            .Verifiable();
        mockAlgorithm2
            .Setup(x => x.Code)
            .Returns("code2")
            .Verifiable();
        mockAlgorithm3
            .Setup(x => x.Code)
            .Returns("code3")
            .Verifiable();

        mockAlgorithm1
            .Setup(x => x.Type)
            .Returns(AlgorithmType.Unspecified)
            .Verifiable();
        mockAlgorithm2
            .Setup(x => x.Type)
            .Returns(AlgorithmType.Compression)
            .Verifiable();
        mockAlgorithm3
            .Setup(x => x.Type)
            .Returns(AlgorithmType.Unspecified)
            .Verifiable();

        var algorithms = new AlgorithmCollection(new[]
        {
            mockAlgorithm1.Object,
            mockAlgorithm2.Object,
            mockAlgorithm3.Object
        });
        Assert.Equal(3, algorithms.Count);

        var result1 = algorithms.TryGetCompressionAlgorithm("code1", out var algorithm1);
        Assert.False(result1);
        Assert.Null(algorithm1);

        var result2 = algorithms.TryGetCompressionAlgorithm("code2", out var algorithm2);
        Assert.True(result2);
        Assert.NotNull(algorithm2);

        var result3 = algorithms.TryGetCompressionAlgorithm("code3", out var algorithm3);
        Assert.False(result3);
        Assert.Null(algorithm3);
    }
}
