// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Disposables;

namespace NCode.Identity.Jose.Tests.Internal;

public class DisposableExtensionsTests : BaseTests
{
    [Fact]
    public void DisposeAll_MultipleDisposables_DisposesAll()
    {
        var mockDisposable1 = CreateStrictMock<IDisposable>();
        var mockDisposable2 = CreateStrictMock<IDisposable>();
        var mockDisposable3 = CreateStrictMock<IDisposable>();

        mockDisposable1.Setup(x => x.Dispose()).Verifiable();
        mockDisposable2.Setup(x => x.Dispose()).Verifiable();
        mockDisposable3.Setup(x => x.Dispose()).Verifiable();

        var disposables = new[]
        {
            mockDisposable1.Object,
            mockDisposable2.Object,
            mockDisposable3.Object,
        };

        disposables.DisposeAll();
    }

    [Fact]
    public void DisposeAll_OneThrowsException_ThrowsFirstException()
    {
        var mockDisposable1 = CreateStrictMock<IDisposable>();
        var mockDisposable2 = CreateStrictMock<IDisposable>();
        var mockDisposable3 = CreateStrictMock<IDisposable>();

        mockDisposable1.Setup(x => x.Dispose()).Verifiable();
        mockDisposable2.Setup(x => x.Dispose()).Throws<InvalidOperationException>().Verifiable();
        mockDisposable3.Setup(x => x.Dispose()).Verifiable();

        var disposables = new[]
        {
            mockDisposable1.Object,
            mockDisposable2.Object,
            mockDisposable3.Object,
        };

        Assert.Throws<InvalidOperationException>(() => disposables.DisposeAll());
    }

    [Fact]
    public void DisposeAll_MultipleThrowExceptions_ThrowsAggregateException()
    {
        var mockDisposable1 = CreateStrictMock<IDisposable>();
        var mockDisposable2 = CreateStrictMock<IDisposable>();
        var mockDisposable3 = CreateStrictMock<IDisposable>();

        mockDisposable1.Setup(x => x.Dispose()).Verifiable();
        mockDisposable2.Setup(x => x.Dispose()).Throws<InvalidOperationException>().Verifiable();
        mockDisposable3.Setup(x => x.Dispose()).Throws<ApplicationException>().Verifiable();

        var disposables = new[]
        {
            mockDisposable1.Object,
            mockDisposable2.Object,
            mockDisposable3.Object,
        };

        var aggregateException = Assert.Throws<AggregateException>(() => disposables.DisposeAll());

        Assert.Contains(aggregateException.InnerExceptions, ex => ex is InvalidOperationException);
        Assert.Contains(aggregateException.InnerExceptions, ex => ex is ApplicationException);
    }
}
