// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose.Cbor;
using NCode.Identity.Cose.Options;

namespace NCode.Identity.Cose.Tests.Cbor;

public class CborGuardTests
{
    private static CoseOptions Options { get; } = new();

    private static byte[] Nested(int depth)
    {
        var bytes = new byte[depth + 1];
        Array.Fill(bytes, (byte)0x81, 0, depth);
        bytes[depth] = 0x00;
        return bytes;
    }

    private static ReadOnlyMemory<byte> ReadValue(byte[] bytes, int maxDepth) =>
        CborGuard.ReadValue(CborGuard.NewReader(bytes), maxDepth);

    #region ReadValue Tests

    [Theory]
    [InlineData("9F01FF")]
    [InlineData("5F4101FF")]
    [InlineData("7F6161FF")]
    [InlineData("BF0101FF")]
    public void ReadValue_IndefiniteLength_Throws(string hex)
    {
        var exception = Assert.Throws<CoseException>(() =>
            ReadValue(Convert.FromHexString(hex), 32)
        );

        Assert.Equal(CoseFailureCodes.InvalidMessage, exception.FailureCode);
    }

    [Fact]
    public void ReadValue_NestingAtTheLimit_ReturnsTheItem()
    {
        var bytes = Nested(32);

        Assert.Equal(bytes, ReadValue(bytes, 32).ToArray());
    }

    [Fact]
    public void ReadValue_NestingBeyondTheLimit_Throws()
    {
        var exception = Assert.Throws<CoseException>(() => ReadValue(Nested(33), 32));

        Assert.Equal(CoseFailureCodes.LimitExceeded, exception.FailureCode);
    }

    [Fact]
    public void ReadValue_TagCountsAsALevel()
    {
        var bytes = Convert.FromHexString("C1C1C101");

        Assert.Equal(
            CoseFailureCodes.LimitExceeded,
            Assert.Throws<CoseException>(() => ReadValue(bytes, 2)).FailureCode
        );
        Assert.Equal(bytes, ReadValue(bytes, 3).ToArray());
    }

    [Fact]
    public void ReadValue_Truncated_ThrowsInvalidMessage()
    {
        var exception = Assert.Throws<CoseException>(() => ReadValue([0x42, 0x01], 32));

        Assert.Equal(CoseFailureCodes.InvalidMessage, exception.FailureCode);
    }

    #endregion

    #region ReadRootMap Tests

    [Fact]
    public void ReadRootMap_IntegerAndTextLabels_ReadsTheEntries()
    {
        var map = CborGuard.ReadRootMap(Convert.FromHexString("A301012002616103"), Options);

        Assert.Equal(3, map.Count);
        Assert.True(map[new CoseLabel(1)].TryGetInt64(out var first));
        Assert.Equal(1, first);
        Assert.True(map[new CoseLabel(-1)].TryGetInt64(out var second));
        Assert.Equal(2, second);
        Assert.True(map[new CoseLabel("a")].TryGetInt64(out var third));
        Assert.Equal(3, third);
    }

    [Theory]
    [InlineData("A201010102", CoseFailureCodes.InvalidMessage)]
    [InlineData("A1F93E0001", CoseFailureCodes.InvalidMessage)]
    [InlineData("A000", CoseFailureCodes.InvalidMessage)]
    [InlineData("01", CoseFailureCodes.InvalidMessage)]
    public void ReadRootMap_Malformed_ThrowsTheCode(string hex, string code)
    {
        var exception = Assert.Throws<CoseException>(() =>
            CborGuard.ReadRootMap(Convert.FromHexString(hex), Options)
        );

        Assert.Equal(code, exception.FailureCode);
    }

    [Fact]
    public void ReadRootMap_DeclaredLengthBeyondTheInput_ThrowsInvalidMessage()
    {
        var exception = Assert.Throws<CoseException>(() =>
            CborGuard.ReadRootMap(Convert.FromHexString("BA7FFFFFFF"), Options)
        );

        Assert.Equal(CoseFailureCodes.InvalidMessage, exception.FailureCode);
    }

    [Fact]
    public void ReadRootMap_InputOverTheSizeLimit_ThrowsLimitExceeded()
    {
        var options = new CoseOptions { MaxMessageSizeBytes = 4 };

        var exception = Assert.Throws<CoseException>(() =>
            CborGuard.ReadRootMap(Convert.FromHexString("A10142AABB"), options)
        );

        Assert.Equal(CoseFailureCodes.LimitExceeded, exception.FailureCode);
    }

    [Fact]
    public void ReadRootMap_ValueNestedBeyondTheLimit_ThrowsLimitExceeded()
    {
        var options = new CoseOptions { MaxNestingDepth = 2 };
        var bytes = Convert.FromHexString("A1018181" + "00");

        var exception = Assert.Throws<CoseException>(() => CborGuard.ReadRootMap(bytes, options));

        Assert.Equal(CoseFailureCodes.LimitExceeded, exception.FailureCode);
    }

    #endregion
}
