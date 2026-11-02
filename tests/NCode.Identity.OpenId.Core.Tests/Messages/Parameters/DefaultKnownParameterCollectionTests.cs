// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Messages.Parameters;

public class DefaultKnownParameterCollectionTests
{
    private static KnownParameter KnownParameter(string name) =>
        new KnownParameter<string>(name, new StringParser()) { AllowMissingStringValues = true };

    private static DefaultKnownParameterCollection CreateCollection() =>
        new([KnownParameter("a"), KnownParameter("b")]);

    #region Count / Enumeration Tests

    [Fact]
    public void Count_ReturnsNumberOfParameters()
    {
        Assert.Equal(2, CreateCollection().Count);
    }

    [Fact]
    public void GetEnumerator_YieldsAllParameters()
    {
        var names = CreateCollection().Select(x => x.Name).OrderBy(x => x).ToList();

        Assert.Equal(["a", "b"], names);
    }

    #endregion

    #region TryGet Tests

    [Fact]
    public void TryGet_WhenPresent_ReturnsTrue()
    {
        var found = CreateCollection().TryGet("a", out var knownParameter);

        Assert.True(found);
        Assert.NotNull(knownParameter);
        Assert.Equal("a", knownParameter.Name);
    }

    [Fact]
    public void TryGet_WhenMissing_ReturnsFalse()
    {
        var found = CreateCollection().TryGet("missing", out var knownParameter);

        Assert.False(found);
        Assert.Null(knownParameter);
    }

    #endregion
}
