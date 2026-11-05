// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Cose.Tests.Messages;

public class CoseMapTests
{
    #region EncodeMap Tests

    [Fact]
    public async Task EncodeMap_MixedLabels_OrdersByEncodedLabelAndRoundTrips()
    {
        await using var host = new CoseTestHost();
        var map = new Dictionary<CoseLabel, CoseValue>
        {
            ["a"] = CoseValue.FromInt64(1),
            [-1] = CoseValue.FromInt64(2),
            [24] = CoseValue.FromInt64(3),
            [1] = CoseValue.FromTextString("x"),
        };

        var encoded = host.Serializer.EncodeMap(map);
        var decoded = host.Serializer.DecodeMap(encoded);

        Assert.Equal("A401617818180320026161" + "01", Convert.ToHexString(encoded));
        Assert.Equal(4, decoded.Count);
        Assert.True(decoded[-1].TryGetInt64(out var second));
        Assert.Equal(2, second);
    }

    [Fact]
    public async Task EncodeMap_IndefiniteValue_Throws()
    {
        await using var host = new CoseTestHost();
        var map = new Dictionary<CoseLabel, CoseValue>
        {
            [1] = CoseValue.FromEncodedValue(CoseTestKeys.Hex("9F01FF")),
        };

        Assert.Throws<ArgumentException>(() => host.Serializer.EncodeMap(map));
    }

    #endregion

    #region DecodeMap Tests

    [Fact]
    public async Task DecodeMap_TrailingBytes_ThrowsInvalidMessage()
    {
        await using var host = new CoseTestHost();

        var exception = Assert.Throws<CoseException>(() =>
            host.Serializer.DecodeMap(CoseTestKeys.Hex("A00000"))
        );

        Assert.Equal(CoseFailureCodes.InvalidMessage, exception.FailureCode);
    }

    #endregion
}
