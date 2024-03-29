﻿#region Copyright Preamble

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

using Microsoft.Extensions.Primitives;
using NCode.Jose.Algorithms;
using NCode.Jose.Infrastructure;

namespace NCode.Jose.Tests.Algorithms;

public class CompositeAlgorithmDataSourceTests : BaseTests
{
    [Fact]
    public void Collection_Initial_Valid()
    {
        var mockAlgorithm1 = CreateStrictMock<Algorithm>();
        var mockAlgorithm2 = CreateStrictMock<Algorithm>();
        var mockAlgorithm3 = CreateStrictMock<Algorithm>();
        var expected = new[]
        {
            mockAlgorithm1.Object,
            mockAlgorithm2.Object,
            mockAlgorithm3.Object
        };

        var mockDataSource1 = CreateStrictMock<IAlgorithmDataSource>();
        mockDataSource1
            .Setup(x => x.GetChangeToken())
            .Returns(NullChangeToken.Singleton)
            .Verifiable();
        mockDataSource1
            .Setup(x => x.Algorithms)
            .Returns(new[] { mockAlgorithm1.Object })
            .Verifiable();

        var mockDataSource2 = CreateStrictMock<IAlgorithmDataSource>();
        mockDataSource2
            .Setup(x => x.GetChangeToken())
            .Returns(NullChangeToken.Singleton)
            .Verifiable();
        mockDataSource2
            .Setup(x => x.Algorithms)
            .Returns(new[] { mockAlgorithm2.Object })
            .Verifiable();

        var mockDataSource3 = CreateStrictMock<IAlgorithmDataSource>();
        mockDataSource3
            .Setup(x => x.GetChangeToken())
            .Returns(NullChangeToken.Singleton)
            .Verifiable();
        mockDataSource3
            .Setup(x => x.Algorithms)
            .Returns(new[] { mockAlgorithm3.Object })
            .Verifiable();

        var dataSources = new[]
        {
            mockDataSource1.Object,
            mockDataSource2.Object,
            mockDataSource3.Object
        };

        using var composite = new CompositeAlgorithmDataSource(dataSources);
        var algorithms = composite.Algorithms;
        Assert.Equal(expected, algorithms);
    }

    [Fact]
    public void Collection_Changed_Valid()
    {
        var mockAlgorithm1 = CreateStrictMock<Algorithm>();
        var mockAlgorithm2 = CreateStrictMock<Algorithm>();

        using var cancellationTokenSource = new CancellationTokenSource();
        var changeTokenSource = new CancellationChangeToken(cancellationTokenSource.Token);

        var mockDataSource1 = CreateStrictMock<IAlgorithmDataSource>();
        mockDataSource1
            .Setup(x => x.GetChangeToken())
            .Returns(() => changeTokenSource.HasChanged ? NullChangeToken.Singleton : changeTokenSource)
            .Verifiable();
        mockDataSource1
            .Setup(x => x.Algorithms)
            .Returns(() => new[] { changeTokenSource.HasChanged ? mockAlgorithm2.Object : mockAlgorithm1.Object })
            .Verifiable();

        var dataSources = new[]
        {
            mockDataSource1.Object
        };

        using var composite = new CompositeAlgorithmDataSource(dataSources);

        var algorithmsBefore = composite.Algorithms;
        Assert.Equal(new[] { mockAlgorithm1.Object }, algorithmsBefore);

        var changeTokenBefore = composite.GetChangeToken();
        Assert.False(changeTokenBefore.HasChanged);

        cancellationTokenSource.Cancel();

        var algorithmsAfter = composite.Algorithms;
        Assert.Equal(new[] { mockAlgorithm2.Object }, algorithmsAfter);

        var changeTokenAfter = composite.GetChangeToken();
        Assert.False(changeTokenAfter.HasChanged);
        Assert.True(changeTokenBefore.HasChanged);
    }

    [Fact]
    public void Disposed_Valid()
    {
        var mockAlgorithm1 = CreateStrictMock<Algorithm>();
        var mockDataSource1 = CreateStrictMock<IAlgorithmDataSource>();
        mockDataSource1
            .Setup(x => x.GetChangeToken())
            .Returns(NullChangeToken.Singleton)
            .Verifiable();
        mockDataSource1
            .Setup(x => x.Algorithms)
            .Returns(() => new[] { mockAlgorithm1.Object })
            .Verifiable();

        var dataSources = new[]
        {
            mockDataSource1.Object
        };

        using var composite = new CompositeAlgorithmDataSource(dataSources);
        composite.Dispose();

        Assert.Throws<ObjectDisposedException>(() => composite.Algorithms);
        Assert.Throws<ObjectDisposedException>(() => composite.GetChangeToken());
    }
}
