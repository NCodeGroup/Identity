#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using Xunit;

namespace NCode.Identity.Secrets.Persistence.GoogleCloudKms.Tests;

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
