// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Messages.Parameters;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Messages.Parameters;

public class CompositeParameterCollectionTests : BaseTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WhenNoSources_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CompositeParameterCollection());
    }

    #endregion

    #region Contains Tests

    [Fact]
    public void Contains_WhenAnySourceContains_ReturnsTrue()
    {
        var source1 = CreateStrictMock<IParameterCollection>();
        var source2 = CreateStrictMock<IParameterCollection>();
        source1.Setup(x => x.Contains("a")).Returns(false).Verifiable();
        source2.Setup(x => x.Contains("a")).Returns(true).Verifiable();

        var collection = new CompositeParameterCollection(source1.Object, source2.Object);

        Assert.True(collection.Contains("a"));
    }

    [Fact]
    public void Contains_WhenNoSourceContains_ReturnsFalse()
    {
        var source = CreateStrictMock<IParameterCollection>();
        source.Setup(x => x.Contains("a")).Returns(false).Verifiable();

        var collection = new CompositeParameterCollection(source.Object);

        Assert.False(collection.Contains("a"));
    }

    #endregion

    #region TryGet Tests

    [Fact]
    public void TryGet_ByName_ReturnsFromFirstMatchingSource()
    {
        var parameter = CreateStrictMock<IParameter>().Object;

        var source1 = CreateStrictMock<IParameterCollection>();
        IParameter? missing = null;
        source1.Setup(x => x.TryGet("a", out missing)).Returns(false).Verifiable();

        var source2 = CreateStrictMock<IParameterCollection>();
        var found = parameter;
        source2.Setup(x => x.TryGet("a", out found)).Returns(true).Verifiable();

        var collection = new CompositeParameterCollection(source1.Object, source2.Object);

        Assert.True(collection.TryGet("a", out var result));
        Assert.Same(parameter, result);
    }

    [Fact]
    public void TryGet_ByName_WhenMissing_ReturnsFalse()
    {
        var source = CreateStrictMock<IParameterCollection>();
        IParameter? missing = null;
        source.Setup(x => x.TryGet("a", out missing)).Returns(false).Verifiable();

        var collection = new CompositeParameterCollection(source.Object);

        Assert.False(collection.TryGet("a", out var result));
        Assert.Null(result);
    }

    #endregion

    #region Remove Tests

    [Fact]
    public void Remove_WhenAnySourceRemoves_ReturnsTrue()
    {
        var source1 = CreateStrictMock<IParameterCollection>();
        var source2 = CreateStrictMock<IParameterCollection>();
        source1.Setup(x => x.Remove("a")).Returns(false).Verifiable();
        source2.Setup(x => x.Remove("a")).Returns(true).Verifiable();

        var collection = new CompositeParameterCollection(source1.Object, source2.Object);

        Assert.True(collection.Remove("a"));
    }

    #endregion

    #region Set Tests

    [Fact]
    public void Set_DelegatesToFirstSource()
    {
        var parameter = CreateStrictMock<IParameter>().Object;

        var source1 = CreateStrictMock<IParameterCollection>();
        source1.Setup(x => x.Set(parameter)).Verifiable();
        var source2 = CreateStrictMock<IParameterCollection>();

        var collection = new CompositeParameterCollection(source1.Object, source2.Object);

        collection.Set(parameter);
    }

    #endregion
}
