// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.Secrets.GoogleCloudKms.Tests;

public class GoogleCloudKmsChecksumTests
{
    private const int TestDataLength = 32;

    #region Compute Tests

    [Fact]
    public void Compute_CheckString_ReturnsCheckValue()
    {
        const long expected = 0xE3069283L;

        var result = GoogleCloudKmsChecksum.Compute("123456789"u8);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Compute_Empty_ReturnsZero()
    {
        const long expected = 0L;

        var result = GoogleCloudKmsChecksum.Compute(ReadOnlySpan<byte>.Empty);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Compute_ZeroBytes_ReturnsKnownValue()
    {
        const long expected = 0x8A9136AAL;
        var data = new byte[TestDataLength];

        var result = GoogleCloudKmsChecksum.Compute(data);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Compute_AllOnesBytes_ReturnsKnownValue()
    {
        const long expected = 0x62A8AB43L;
        var data = new byte[TestDataLength];
        Array.Fill(data, byte.MaxValue);

        var result = GoogleCloudKmsChecksum.Compute(data);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Compute_IncrementingBytes_ReturnsKnownValue()
    {
        const long expected = 0x46DD794EL;
        var data = new byte[TestDataLength];
        for (var index = 0; index < data.Length; index++)
        {
            data[index] = (byte)index;
        }

        var result = GoogleCloudKmsChecksum.Compute(data);

        Assert.Equal(expected, result);
    }

    #endregion
}
