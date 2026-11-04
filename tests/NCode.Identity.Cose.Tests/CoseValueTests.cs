// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Cose.Tests;

public class CoseValueTests
{
    private static CoseValue FromHex(string hex) =>
        CoseValue.FromEncodedValue(Convert.FromHexString(hex));

    #region Factories

    [Fact]
    public void FromInt64_RoundTripsThroughTryGetInt64()
    {
        var value = CoseValue.FromInt64(-7);

        Assert.True(value.TryGetInt64(out var result));
        Assert.Equal(-7, result);
        Assert.Equal("26", Convert.ToHexString(value.EncodedValue.Span));
    }

    [Fact]
    public void FromTextString_RoundTripsThroughTryGetTextString()
    {
        var value = CoseValue.FromTextString("abc");

        Assert.True(value.TryGetTextString(out var result));
        Assert.Equal("abc", result);
    }

    [Fact]
    public void FromByteString_RoundTripsThroughTryGetByteString()
    {
        var value = CoseValue.FromByteString([1, 2, 3]);

        Assert.True(value.TryGetByteString(out var result));
        Assert.Equal(new byte[] { 1, 2, 3 }, result.ToArray());
    }

    [Fact]
    public void FromLabels_IntegerAndText_EncodesAnArray()
    {
        var value = CoseValue.FromLabels([1, "a"]);

        Assert.Equal("82016161", Convert.ToHexString(value.EncodedValue.Span));
    }

    [Fact]
    public void FromEncodedValue_TwoItems_Throws()
    {
        Assert.Throws<ArgumentException>(() => FromHex("0102"));
    }

    [Fact]
    public void FromEncodedValue_TruncatedItem_Throws()
    {
        Assert.Throws<ArgumentException>(() => FromHex("4201"));
    }

    [Fact]
    public void FromEncodedValue_InvalidUtf8Text_Throws()
    {
        Assert.Throws<ArgumentException>(() => FromHex("62C328"));
    }

    #endregion

    #region Getters

    [Fact]
    public void Getters_AnotherType_ReturnFalse()
    {
        var text = CoseValue.FromTextString("a");

        Assert.False(text.TryGetInt64(out _));
        Assert.False(text.TryGetByteString(out _));
        Assert.False(text.TryGetDouble(out _));
        Assert.False(text.TryGetArray(out _));
        Assert.False(text.IsMap);
        Assert.False(CoseValue.FromInt64(1).TryGetTextString(out _));
    }

    [Fact]
    public void Getters_TaggedItem_ReturnFalse()
    {
        var tagged = FromHex("C101");

        Assert.False(tagged.TryGetInt64(out _));
    }

    [Fact]
    public void TryGetInt64_BeyondTheLongRange_ReturnsFalse()
    {
        Assert.False(FromHex("1BFFFFFFFFFFFFFFFF").TryGetInt64(out _));
    }

    [Fact]
    public void TryGetDouble_HalfAndDoublePrecision_ReadTheNumber()
    {
        Assert.True(FromHex("F93E00").TryGetDouble(out var half));
        Assert.Equal(1.5, half);

        Assert.True(FromHex("FB41D584367C200000").TryGetDouble(out var wide));
        Assert.Equal(1443944944.5, wide);
    }

    [Fact]
    public void TryGetArray_DefiniteArray_ReadsEachElement()
    {
        Assert.True(FromHex("820102").TryGetArray(out var elements));

        Assert.Equal(2, elements.Count);
        Assert.True(elements[1].TryGetInt64(out var second));
        Assert.Equal(2, second);
    }

    [Fact]
    public void IsMap_Map_ReturnsTrue()
    {
        Assert.True(FromHex("A0").IsMap);
    }

    #endregion
}
