// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.RemoteKeys;

public class RemoteKeyRefusalsTests : BaseTests
{
    #region CreatesNoKeys Tests

    [Fact]
    public void CreatesNoKeys_WhenGivenAProviderName_NamesItInTheReason()
    {
        Assert.Equal(
            "The remote key provider 'aws-kms' creates no keys: it has no key-creation location.",
            RemoteKeyRefusals.CreatesNoKeys(RemoteKeyProviderNames.AwsKms)
        );
    }

    #endregion
}
