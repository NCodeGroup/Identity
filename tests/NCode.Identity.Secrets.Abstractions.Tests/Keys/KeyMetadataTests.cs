// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Keys;

public class KeyMetadataTests
{
    #region Property Tests

    [Fact]
    public void Properties_WhenSet_RoundTrip()
    {
        var activatesWhen = new DateTimeOffset(2029, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var retiresWhen = new DateTimeOffset(2029, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var expiresWhen = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var metadata = new KeyMetadata
        {
            KeyId = "kid",
            Use = "sig",
            Algorithm = "RS256",
            ActivatesWhen = activatesWhen,
            RetiresWhen = retiresWhen,
            ExpiresWhen = expiresWhen,
        };

        Assert.Equal("kid", metadata.KeyId);
        Assert.Equal("sig", metadata.Use);
        Assert.Equal("RS256", metadata.Algorithm);
        Assert.Equal(activatesWhen, metadata.ActivatesWhen);
        Assert.Equal(retiresWhen, metadata.RetiresWhen);
        Assert.Equal(expiresWhen, metadata.ExpiresWhen);
    }

    [Fact]
    public void Defaults_WhenNotSet_AreNull()
    {
        var metadata = new KeyMetadata();

        Assert.Null(metadata.KeyId);
        Assert.Null(metadata.Use);
        Assert.Null(metadata.Algorithm);
        Assert.Null(metadata.ActivatesWhen);
        Assert.Null(metadata.RetiresWhen);
        Assert.Null(metadata.ExpiresWhen);
    }

    [Fact]
    public void Equality_WhenSameValues_AreEqual()
    {
        var left = new KeyMetadata { KeyId = "kid", Use = "sig" };
        var right = new KeyMetadata { KeyId = "kid", Use = "sig" };

        Assert.Equal(left, right);
    }

    #endregion
}
