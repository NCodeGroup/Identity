// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.RemoteKeys;

public class RemoteKeyProviderDescriptorTests : BaseTests
{
    #region CreatesKeys Property Tests

    [Fact]
    public void CreatesKeys_WhenNotSet_IsTrue()
    {
        var descriptor = new RemoteKeyProviderDescriptor { Name = RemoteKeyProviderNames.AwsKms };

        Assert.True(descriptor.CreatesKeys);
    }

    [Fact]
    public void CreatesKeys_WhenSetToFalse_IsFalse()
    {
        var descriptor = new RemoteKeyProviderDescriptor
        {
            Name = RemoteKeyProviderNames.AwsKms,
            CreatesKeys = false,
        };

        Assert.False(descriptor.CreatesKeys);
    }

    #endregion
}
