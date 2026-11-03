// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using NCode.Identity.Jose.EdDsa.Keys;
using NCode.Identity.Jose.EdDsa.KeyTypes;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.Jose.EdDsa.Tests.Keys;

public class PublicOkpSecretKeyTests
{
    #region HasPrivateKey Property Tests

    [Fact]
    public void HasPrivateKey_ReturnsFalse()
    {
        var secretKey = new PublicOkpSecretKey(default, OkpCurves.X25519, new byte[32]);

        Assert.False(secretKey.HasPrivateKey);
    }

    #endregion

    #region ExportPrivateKey Tests

    [Theory]
    [InlineData(OkpCurves.Ed25519Name)]
    [InlineData(OkpCurves.X25519Name)]
    public void ExportPrivateKey_WhenPublicHalfOnly_ThrowsAndWritesNothing(string curveName)
    {
        Assert.True(OkpCurves.TryGetByName(curveName, out var curve));
        var secretKey = new PublicOkpSecretKey(
            new KeyMetadata { KeyId = "public" },
            curve,
            new byte[curve.PublicKeySize]
        );
        var destination = new ArrayBufferWriter<byte>();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            secretKey.ExportPrivateKey(ref destination)
        );

        Assert.Equal(
            "This OKP secret key holds only the public half and cannot export a private key.",
            exception.Message
        );
        Assert.Equal(0, destination.WrittenCount);
    }

    #endregion
}
