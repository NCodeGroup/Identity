// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Keys;

public class EmptySecretKeyTests
{
    #region Singleton Tests

    [Fact]
    public void Singleton_WhenAccessed_ReturnsSameInstance()
    {
        Assert.Same(EmptySecretKey.Singleton, EmptySecretKey.Singleton);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Properties_WhenEmpty_ReturnDefaults()
    {
        var key = EmptySecretKey.Singleton;

        Assert.Equal(string.Empty, key.KeyType);
        Assert.Equal(default, key.Metadata);
        Assert.Equal(0, key.KeySizeBits);
        Assert.Equal(0, key.KeySizeBytes);
        Assert.Null(key.KeyId);
    }

    #endregion
}
