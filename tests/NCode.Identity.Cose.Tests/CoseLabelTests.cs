// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Cose.Tests;

public class CoseLabelTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_Integer_IsNotText()
    {
        var label = new CoseLabel(-7);

        Assert.False(label.IsText);
        Assert.Equal(-7, label.IntegerValue);
        Assert.Null(label.TextValue);
    }

    [Fact]
    public void Constructor_Text_IsText()
    {
        var label = new CoseLabel("a");

        Assert.True(label.IsText);
        Assert.Equal("a", label.TextValue);
        Assert.Equal(0, label.IntegerValue);
    }

    [Fact]
    public void Constructor_NullText_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CoseLabel((string)null!));
    }

    #endregion

    #region ImplicitConversion Tests

    [Fact]
    public void ImplicitConversion_FromLongAndString_BuildsLabels()
    {
        CoseLabel integer = 4L;
        CoseLabel text = "kid";

        Assert.Equal(new CoseLabel(4), integer);
        Assert.Equal(new CoseLabel("kid"), text);
    }

    #endregion

    #region Equals Tests

    [Fact]
    public void Equals_IntegerAndTextWithSameDigits_AreDifferent()
    {
        Assert.NotEqual(new CoseLabel(1), new CoseLabel("1"));
    }

    [Fact]
    public void Equals_SameLabels_AreEqualAndHashEqual()
    {
        Assert.Equal(new CoseLabel("a"), new CoseLabel("a"));
        Assert.Equal(new CoseLabel("a").GetHashCode(), new CoseLabel("a").GetHashCode());
        Assert.Equal(new CoseLabel(9), new CoseLabel(9));
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_IntegerAndText_ReturnsTheLabel()
    {
        Assert.Equal("-7", new CoseLabel(-7).ToString());
        Assert.Equal("kid", new CoseLabel("kid").ToString());
    }

    #endregion
}
