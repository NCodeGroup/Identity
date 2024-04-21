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

using NCode.Disposables;

namespace NCode.Jose.Tests.Internal;

public class DisposableExtensionsTests : BaseTests
{
    [Fact]
    public void DisposeAll_Valid()
    {
        var mockDisposable1 = CreateStrictMock<IDisposable>();
        var mockDisposable2 = CreateStrictMock<IDisposable>();
        var mockDisposable3 = CreateStrictMock<IDisposable>();

        mockDisposable1
            .Setup(x => x.Dispose())
            .Verifiable();
        mockDisposable2
            .Setup(x => x.Dispose())
            .Verifiable();
        mockDisposable3
            .Setup(x => x.Dispose())
            .Verifiable();

        var disposables = new[]
        {
            mockDisposable1.Object,
            mockDisposable2.Object,
            mockDisposable3.Object
        };

        disposables.DisposeAll();
    }

    [Fact]
    public void DisposeAll_ThrowsSingle_Valid()
    {
        var mockDisposable1 = CreateStrictMock<IDisposable>();
        var mockDisposable2 = CreateStrictMock<IDisposable>();
        var mockDisposable3 = CreateStrictMock<IDisposable>();

        mockDisposable1
            .Setup(x => x.Dispose())
            .Verifiable();
        mockDisposable2
            .Setup(x => x.Dispose())
            .Throws<InvalidOperationException>()
            .Verifiable();
        mockDisposable3
            .Setup(x => x.Dispose())
            .Verifiable();

        var disposables = new[]
        {
            mockDisposable1.Object,
            mockDisposable2.Object,
            mockDisposable3.Object
        };

        Assert.Throws<InvalidOperationException>(() =>
            disposables.DisposeAll());
    }

    [Fact]
    public void DisposeAll_ThrowsMultiple_Valid()
    {
        var mockDisposable1 = CreateStrictMock<IDisposable>();
        var mockDisposable2 = CreateStrictMock<IDisposable>();
        var mockDisposable3 = CreateStrictMock<IDisposable>();

        mockDisposable1
            .Setup(x => x.Dispose())
            .Verifiable();
        mockDisposable2
            .Setup(x => x.Dispose())
            .Throws<InvalidOperationException>()
            .Verifiable();
        mockDisposable3
            .Setup(x => x.Dispose())
            .Throws<ApplicationException>()
            .Verifiable();

        var disposables = new[]
        {
            mockDisposable1.Object,
            mockDisposable2.Object,
            mockDisposable3.Object
        };

        var aggregateException = Assert.Throws<AggregateException>(() =>
            disposables.DisposeAll());

        Assert.Contains(aggregateException.InnerExceptions, ex => ex is InvalidOperationException);
        Assert.Contains(aggregateException.InnerExceptions, ex => ex is ApplicationException);
    }
}
