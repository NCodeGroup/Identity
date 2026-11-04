// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose.Cbor;

namespace NCode.Identity.Cose.Tests.Cbor;

public class CborMapWriterTests
{
    #region EncodeMap

    [Fact]
    public void EncodeMap_MixedLabels_OrdersByTheBytesOfTheEncodedLabel()
    {
        var map = new Dictionary<CoseLabel, CoseValue>
        {
            ["a"] = CoseValue.FromInt64(1),
            [-1] = CoseValue.FromInt64(1),
            [24] = CoseValue.FromInt64(1),
            [1] = CoseValue.FromInt64(1),
        };

        var encoded = CborMapWriter.EncodeMap(map);

        Assert.Equal("A401011818012001616101", Convert.ToHexString(encoded));
    }

    #endregion

    #region EncodeProtected

    [Fact]
    public void EncodeProtected_EmptyBucket_ReturnsNoBytes()
    {
        Assert.Empty(CborMapWriter.EncodeProtected(new Dictionary<CoseLabel, CoseValue>()));
    }

    [Fact]
    public void EncodeProtected_OneParameter_ReturnsTheEncodedMap()
    {
        var map = new Dictionary<CoseLabel, CoseValue> { [1] = CoseValue.FromInt64(-7) };

        Assert.Equal("A10126", Convert.ToHexString(CborMapWriter.EncodeProtected(map)));
    }

    #endregion

    #region ValidateValue

    [Fact]
    public void ValidateValue_IndefiniteLength_Throws()
    {
        var value = CoseValue.FromEncodedValue(Convert.FromHexString("9F01FF"));

        Assert.Throws<ArgumentException>(() => CborMapWriter.ValidateValue(value, 32));
    }

    [Fact]
    public void ValidateValue_DefiniteLength_Passes()
    {
        CborMapWriter.ValidateValue(CoseValue.FromLabels([1, "a"]), 32);
    }

    #endregion
}
