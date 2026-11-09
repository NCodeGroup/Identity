// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Keys;

public class KeyMetadataExtensionsTests
{
    private static DateTimeOffset Now { get; } = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    #region IsActive Tests

    [Fact]
    public void IsActive_WhenNoInstantIsSet_IsTrue()
    {
        var metadata = new KeyMetadata();

        Assert.True(metadata.IsActive(Now));
    }

    [Fact]
    public void IsActive_WhenNowIsInsideTheActiveWindow_IsTrue()
    {
        var metadata = new KeyMetadata
        {
            ActivatesWhen = Now.AddDays(-1),
            RetiresWhen = Now.AddDays(1),
            ExpiresWhen = Now.AddDays(2),
        };

        Assert.True(metadata.IsActive(Now));
    }

    [Fact]
    public void IsActive_WhenTheKeyActivatesNow_IsTrue()
    {
        var metadata = new KeyMetadata { ActivatesWhen = Now };

        Assert.True(metadata.IsActive(Now));
    }

    [Fact]
    public void IsActive_WhenTheKeyActivatesLater_IsFalse()
    {
        var metadata = new KeyMetadata { ActivatesWhen = Now.AddSeconds(1) };

        Assert.False(metadata.IsActive(Now));
    }

    [Fact]
    public void IsActive_WhenTheKeyRetiresNow_IsFalse()
    {
        var metadata = new KeyMetadata { RetiresWhen = Now };

        Assert.False(metadata.IsActive(Now));
    }

    [Fact]
    public void IsActive_WhenTheKeyRetiresLater_IsTrue()
    {
        var metadata = new KeyMetadata { RetiresWhen = Now.AddSeconds(1) };

        Assert.True(metadata.IsActive(Now));
    }

    [Fact]
    public void IsActive_WhenTheKeyHasExpiredWithoutRetiring_IsFalse()
    {
        var metadata = new KeyMetadata { ExpiresWhen = Now };

        Assert.False(metadata.IsActive(Now));
    }

    #endregion

    #region IsExpired Tests

    [Fact]
    public void IsExpired_WhenNoExpiryIsSet_IsFalse()
    {
        var metadata = new KeyMetadata();

        Assert.False(metadata.IsExpired(Now));
    }

    [Fact]
    public void IsExpired_WhenTheKeyExpiresNow_IsTrue()
    {
        var metadata = new KeyMetadata { ExpiresWhen = Now };

        Assert.True(metadata.IsExpired(Now));
    }

    [Fact]
    public void IsExpired_WhenTheKeyExpiresLater_IsFalse()
    {
        var metadata = new KeyMetadata { ExpiresWhen = Now.AddSeconds(1) };

        Assert.False(metadata.IsExpired(Now));
    }

    [Fact]
    public void IsExpired_WhenTheKeyIsRetiredButNotExpired_IsFalse()
    {
        var metadata = new KeyMetadata
        {
            RetiresWhen = Now.AddDays(-1),
            ExpiresWhen = Now.AddDays(1),
        };

        Assert.False(metadata.IsExpired(Now));
    }

    #endregion
}
