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

using NCode.Jose.AuthenticatedEncryption;
using NCode.Jose.Compression;
using NCode.Jose.KeyManagement;
using NCode.Jose.Signature;

namespace NCode.Jose.Tests;

public class AlgorithmProviderTests : BaseTests
{
    private Mock<IAlgorithmFilter> MockAlgorithmFilter { get; }
    private Mock<IAlgorithm> MockAlgorithm1 { get; }
    private Mock<IAlgorithm> MockAlgorithm2 { get; }
    private Mock<IAlgorithm> MockAlgorithm3 { get; }

    public AlgorithmProviderTests()
    {
        MockAlgorithmFilter = CreateStrictMock<IAlgorithmFilter>();
        MockAlgorithm1 = CreateStrictMock<IAlgorithm>();
        MockAlgorithm2 = CreateStrictMock<IAlgorithm>();
        MockAlgorithm3 = CreateStrictMock<IAlgorithm>();

        MockAlgorithm1
            .Setup(_ => _.Code)
            .Returns("code1")
            .Verifiable();

        MockAlgorithm2
            .Setup(_ => _.Code)
            .Returns("code2")
            .Verifiable();

        MockAlgorithm3
            .Setup(_ => _.Code)
            .Returns("code3")
            .Verifiable();

        MockAlgorithm1
            .Setup(_ => _.Type)
            .Returns(AlgorithmType.Unspecified)
            .Verifiable();

        MockAlgorithm2
            .Setup(_ => _.Type)
            .Returns(AlgorithmType.Unspecified)
            .Verifiable();

        MockAlgorithm3
            .Setup(_ => _.Type)
            .Returns(AlgorithmType.Unspecified)
            .Verifiable();
    }

    private AlgorithmProvider CreateProvider()
    {
        var algorithms = new[]
        {
            MockAlgorithm1.Object,
            MockAlgorithm2.Object,
            MockAlgorithm3.Object
        };
        var filters = new[]
        {
            MockAlgorithmFilter.Object
        };
        return new AlgorithmProvider(algorithms, filters);
    }

    [Fact]
    public void Algorithms_Valid()
    {
        MockAlgorithmFilter
            .Setup(_ => _.Exclude(MockAlgorithm1.Object))
            .Returns(false)
            .Verifiable();

        MockAlgorithmFilter
            .Setup(_ => _.Exclude(MockAlgorithm2.Object))
            .Returns(true)
            .Verifiable();

        MockAlgorithmFilter
            .Setup(_ => _.Exclude(MockAlgorithm3.Object))
            .Returns(false)
            .Verifiable();

        var provider = CreateProvider();
        var algorithms = provider.Algorithms;
        Assert.Contains(MockAlgorithm1.Object, algorithms);
        Assert.DoesNotContain(MockAlgorithm2.Object, algorithms);
        Assert.Contains(MockAlgorithm3.Object, algorithms);
    }

    [Fact]
    public void TryGetSignatureAlgorithm_Valid()
    {
        MockAlgorithm2
            .Setup(_ => _.Type)
            .Returns(AlgorithmType.DigitalSignature)
            .Verifiable();

        MockAlgorithm2
            .As<ISignatureAlgorithm>();

        MockAlgorithmFilter
            .Setup(_ => _.Exclude(MockAlgorithm1.Object))
            .Returns(false)
            .Verifiable();

        MockAlgorithmFilter
            .Setup(_ => _.Exclude(MockAlgorithm2.Object))
            .Returns(false)
            .Verifiable();

        MockAlgorithmFilter
            .Setup(_ => _.Exclude(MockAlgorithm3.Object))
            .Returns(false)
            .Verifiable();

        var provider = CreateProvider();
        var algorithms = provider.Algorithms;
        Assert.Equal(3, algorithms.Count);

        var result1 = provider.TryGetSignatureAlgorithm("code1", out var algorithm1);
        Assert.False(result1);
        Assert.Null(algorithm1);

        var result2 = provider.TryGetSignatureAlgorithm("code2", out var algorithm2);
        Assert.True(result2);
        Assert.NotNull(algorithm2);

        var result3 = provider.TryGetSignatureAlgorithm("code3", out var algorithm3);
        Assert.False(result3);
        Assert.Null(algorithm3);
    }

    [Fact]
    public void TryGetKeyManagementAlgorithm_Valid()
    {
        MockAlgorithm2
            .Setup(_ => _.Type)
            .Returns(AlgorithmType.KeyManagement)
            .Verifiable();

        MockAlgorithm2
            .As<IKeyManagementAlgorithm>();

        MockAlgorithmFilter
            .Setup(_ => _.Exclude(MockAlgorithm1.Object))
            .Returns(false)
            .Verifiable();

        MockAlgorithmFilter
            .Setup(_ => _.Exclude(MockAlgorithm2.Object))
            .Returns(false)
            .Verifiable();

        MockAlgorithmFilter
            .Setup(_ => _.Exclude(MockAlgorithm3.Object))
            .Returns(false)
            .Verifiable();

        var provider = CreateProvider();
        var algorithms = provider.Algorithms;
        Assert.Equal(3, algorithms.Count);

        var result1 = provider.TryGetKeyManagementAlgorithm("code1", out var algorithm1);
        Assert.False(result1);
        Assert.Null(algorithm1);

        var result2 = provider.TryGetKeyManagementAlgorithm("code2", out var algorithm2);
        Assert.True(result2);
        Assert.NotNull(algorithm2);

        var result3 = provider.TryGetKeyManagementAlgorithm("code3", out var algorithm3);
        Assert.False(result3);
        Assert.Null(algorithm3);
    }

    [Fact]
    public void TryGetAuthenticatedEncryptionAlgorithm_Valid()
    {
        MockAlgorithm2
            .Setup(_ => _.Type)
            .Returns(AlgorithmType.AuthenticatedEncryption)
            .Verifiable();

        MockAlgorithm2
            .As<IAuthenticatedEncryptionAlgorithm>();

        MockAlgorithmFilter
            .Setup(_ => _.Exclude(MockAlgorithm1.Object))
            .Returns(false)
            .Verifiable();

        MockAlgorithmFilter
            .Setup(_ => _.Exclude(MockAlgorithm2.Object))
            .Returns(false)
            .Verifiable();

        MockAlgorithmFilter
            .Setup(_ => _.Exclude(MockAlgorithm3.Object))
            .Returns(false)
            .Verifiable();

        var provider = CreateProvider();
        var algorithms = provider.Algorithms;
        Assert.Equal(3, algorithms.Count);

        var result1 = provider.TryGetAuthenticatedEncryptionAlgorithm("code1", out var algorithm1);
        Assert.False(result1);
        Assert.Null(algorithm1);

        var result2 = provider.TryGetAuthenticatedEncryptionAlgorithm("code2", out var algorithm2);
        Assert.True(result2);
        Assert.NotNull(algorithm2);

        var result3 = provider.TryGetAuthenticatedEncryptionAlgorithm("code3", out var algorithm3);
        Assert.False(result3);
        Assert.Null(algorithm3);
    }

    [Fact]
    public void TryGetCompressionAlgorithm_Valid()
    {
        MockAlgorithm2
            .Setup(_ => _.Type)
            .Returns(AlgorithmType.Compression)
            .Verifiable();

        MockAlgorithm2
            .As<ICompressionAlgorithm>();

        MockAlgorithmFilter
            .Setup(_ => _.Exclude(MockAlgorithm1.Object))
            .Returns(false)
            .Verifiable();

        MockAlgorithmFilter
            .Setup(_ => _.Exclude(MockAlgorithm2.Object))
            .Returns(false)
            .Verifiable();

        MockAlgorithmFilter
            .Setup(_ => _.Exclude(MockAlgorithm3.Object))
            .Returns(false)
            .Verifiable();

        var provider = CreateProvider();
        var algorithms = provider.Algorithms;
        Assert.Equal(3, algorithms.Count);

        var result1 = provider.TryGetCompressionAlgorithm("code1", out var algorithm1);
        Assert.False(result1);
        Assert.Null(algorithm1);

        var result2 = provider.TryGetCompressionAlgorithm("code2", out var algorithm2);
        Assert.True(result2);
        Assert.NotNull(algorithm2);

        var result3 = provider.TryGetCompressionAlgorithm("code3", out var algorithm3);
        Assert.False(result3);
        Assert.Null(algorithm3);
    }
}
