// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Identity.Secrets.KeyMaterial;

public class KeySizesUtilityTests
{
    #region IsLegalSize (single) Tests

    [Fact]
    public void IsLegalSize_WhenSkipSizeZeroAndMatchesMin_ReturnsTrue()
    {
        var legalSize = new KeySizes(minSize: 256, maxSize: 256, skipSize: 0);

        Assert.True(KeySizesUtility.IsLegalSize(legalSize, 256));
    }

    [Fact]
    public void IsLegalSize_WhenSkipSizeZeroAndDoesNotMatchMin_ReturnsFalse()
    {
        var legalSize = new KeySizes(minSize: 256, maxSize: 256, skipSize: 0);

        Assert.False(KeySizesUtility.IsLegalSize(legalSize, 128));
    }

    [Theory]
    [InlineData(1024, true)]
    [InlineData(2048, true)]
    [InlineData(4096, true)]
    [InlineData(1536, false)]
    [InlineData(512, false)]
    [InlineData(8192, false)]
    public void IsLegalSize_WhenWithinRangeAndOnStep_ReturnsExpected(int size, bool expected)
    {
        var legalSize = new KeySizes(minSize: 1024, maxSize: 4096, skipSize: 1024);

        Assert.Equal(expected, KeySizesUtility.IsLegalSize(legalSize, size));
    }

    #endregion

    #region IsLegalSize (collection) Tests

    [Fact]
    public void IsLegalSize_WhenMatchesAnyRange_ReturnsTrue()
    {
        var legalSizes = new[]
        {
            new KeySizes(minSize: 128, maxSize: 128, skipSize: 0),
            new KeySizes(minSize: 256, maxSize: 256, skipSize: 0),
        };

        Assert.True(KeySizesUtility.IsLegalSize(legalSizes, 256));
    }

    [Fact]
    public void IsLegalSize_WhenMatchesNoRange_ReturnsFalse()
    {
        var legalSizes = new[]
        {
            new KeySizes(minSize: 128, maxSize: 128, skipSize: 0),
            new KeySizes(minSize: 256, maxSize: 256, skipSize: 0),
        };

        Assert.False(KeySizesUtility.IsLegalSize(legalSizes, 512));
    }

    #endregion
}
