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

using NCode.Jose.Algorithms;
using NCode.Jose.Algorithms.AuthenticatedEncryption;
using NCode.Jose.Algorithms.Compression;
using NCode.Jose.Algorithms.KeyManagement;
using NCode.Jose.Algorithms.Signature;

namespace NCode.Jose.Tests.Algorithms;

public class AlgorithmCollectionTests : BaseTests
{
    private Mock<IAlgorithm> MockAlgorithm1 { get; }
    private Mock<IAlgorithm> MockAlgorithm2 { get; }
    private Mock<IAlgorithm> MockAlgorithm3 { get; }

    public AlgorithmCollectionTests()
    {
        MockAlgorithm1 = CreateStrictMock<IAlgorithm>();
        MockAlgorithm2 = CreateStrictMock<IAlgorithm>();
        MockAlgorithm3 = CreateStrictMock<IAlgorithm>();

        MockAlgorithm1
            .Setup(x => x.Code)
            .Returns("code1")
            .Verifiable();

        MockAlgorithm2
            .Setup(x => x.Code)
            .Returns("code2")
            .Verifiable();

        MockAlgorithm3
            .Setup(x => x.Code)
            .Returns("code3")
            .Verifiable();

        MockAlgorithm1
            .Setup(x => x.Type)
            .Returns(AlgorithmType.Unspecified)
            .Verifiable();

        MockAlgorithm2
            .Setup(x => x.Type)
            .Returns(AlgorithmType.Unspecified)
            .Verifiable();

        MockAlgorithm3
            .Setup(x => x.Type)
            .Returns(AlgorithmType.Unspecified)
            .Verifiable();
    }

    private AlgorithmCollection CreateCollection()
    {
        var algorithms = new[]
        {
            MockAlgorithm1.Object,
            MockAlgorithm2.Object,
            MockAlgorithm3.Object
        };
        return new AlgorithmCollection(algorithms);
    }

    [Fact]
    public void Algorithms_Valid()
    {
        var algorithms = CreateCollection();
        Assert.Contains(MockAlgorithm1.Object, algorithms);
        Assert.Contains(MockAlgorithm2.Object, algorithms);
        Assert.Contains(MockAlgorithm3.Object, algorithms);
    }

    [Fact]
    public void TryGetSignatureAlgorithm_Valid()
    {
        MockAlgorithm2
            .Setup(x => x.Type)
            .Returns(AlgorithmType.DigitalSignature)
            .Verifiable();

        MockAlgorithm2
            .As<ISignatureAlgorithm>();

        var algorithms = CreateCollection();
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
        MockAlgorithm2
            .Setup(x => x.Type)
            .Returns(AlgorithmType.KeyManagement)
            .Verifiable();

        MockAlgorithm2
            .As<IKeyManagementAlgorithm>();

        var algorithms = CreateCollection();
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
        MockAlgorithm2
            .Setup(x => x.Type)
            .Returns(AlgorithmType.AuthenticatedEncryption)
            .Verifiable();

        MockAlgorithm2
            .As<IAuthenticatedEncryptionAlgorithm>();

        var algorithms = CreateCollection();
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
        MockAlgorithm2
            .Setup(x => x.Type)
            .Returns(AlgorithmType.Compression)
            .Verifiable();

        MockAlgorithm2
            .As<ICompressionAlgorithm>();

        var algorithms = CreateCollection();
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
