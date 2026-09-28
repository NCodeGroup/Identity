#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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

using System.Security.Cryptography;

namespace NCode.Identity.Secrets.Logic;

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
