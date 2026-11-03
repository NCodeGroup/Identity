// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using NCode.Identity.Jose.EdDsa.Keys;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.Jose.EdDsa.Tests.Keys;

public class OkpSecretKeyTests
{
    #region Scaffolding

    // An OKP key of another package, which may name a curve this package has no encoding for.
    private sealed class StubOkpSecretKey(string curve)
        : OkpSecretKey(
            new KeyMetadata { KeyId = "stub" },
            curve,
            keySizeBits: 256,
            new byte[32],
            certificateRawData: null
        )
    {
        public override bool HasPrivateKey => false;

        public override void ExportPrivateKey<TWriter>(ref TWriter destination) =>
            throw new NotSupportedException();
    }

    #endregion

    #region ExportSubjectPublicKeyInfo Tests

    // RFC 8410 §3: the SubjectPublicKeyInfo names the curve by its OID, which an unknown curve does not have.
    [Fact]
    public void ExportSubjectPublicKeyInfo_WhenCurveIsNotSupported_Throws()
    {
        var secretKey = new StubOkpSecretKey("X-Unknown");
        var destination = new ArrayBufferWriter<byte>();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            secretKey.ExportSubjectPublicKeyInfo(ref destination)
        );

        Assert.Equal("The OKP curve 'X-Unknown' is not supported.", exception.Message);
        Assert.Equal(0, destination.WrittenCount);
    }

    #endregion
}
