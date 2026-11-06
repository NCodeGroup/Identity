// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Sessions;
using Xunit;

namespace NCode.Identity.Bff.Tests.Sessions;

public class DefaultBffConnectionSessionAccessorTests : BaseTests
{
    #region Use Tests

    [Fact]
    public void Current_NothingUsed_IsNull()
    {
        var accessor = new DefaultBffConnectionSessionAccessor();

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void Use_SetsCurrent_DisposeRestoresPrevious()
    {
        var accessor = new DefaultBffConnectionSessionAccessor();
        var first = CreateStrictMock<IBffConnectionSession>().Object;
        var second = CreateStrictMock<IBffConnectionSession>().Object;

        var firstScope = accessor.Use(first);
        Assert.Same(first, accessor.Current);

        var secondScope = accessor.Use(second);
        Assert.Same(second, accessor.Current);

        secondScope.Dispose();
        Assert.Same(first, accessor.Current);

        firstScope.Dispose();
        Assert.Null(accessor.Current);
    }

    [Fact]
    public void Use_DisposeTwice_RestoresOnce()
    {
        var accessor = new DefaultBffConnectionSessionAccessor();
        var first = CreateStrictMock<IBffConnectionSession>().Object;
        var second = CreateStrictMock<IBffConnectionSession>().Object;
        var firstScope = accessor.Use(first);
        var secondScope = accessor.Use(second);

        secondScope.Dispose();
        firstScope.Dispose();
        secondScope.Dispose();

        Assert.Null(accessor.Current);
    }

    [Fact]
    public async Task Use_FlowsIntoAwaitedCalls()
    {
        var accessor = new DefaultBffConnectionSessionAccessor();
        var session = CreateStrictMock<IBffConnectionSession>().Object;

        using (accessor.Use(session))
        {
            var seen = await ReadCurrentAsync(accessor);

            Assert.Same(session, seen);
        }

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void Use_NullSession_Throws()
    {
        var accessor = new DefaultBffConnectionSessionAccessor();

        Assert.Throws<ArgumentNullException>(() => accessor.Use(null!));
    }

    #endregion

    private static async Task<IBffConnectionSession?> ReadCurrentAsync(
        IBffConnectionSessionAccessor accessor
    )
    {
        await Task.Yield();
        return accessor.Current;
    }
}
