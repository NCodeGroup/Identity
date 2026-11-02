// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Tests.SecretKeys;

public class SecretKeyExpiresWhenComparerTests : BaseTests
{
    private Mock<SecretKey> MockSecretKeyLeft { get; }
    private Mock<SecretKey> MockSecretKeyRight { get; }

    public SecretKeyExpiresWhenComparerTests()
    {
        MockSecretKeyLeft = CreateStrictMock<SecretKey>();
        MockSecretKeyRight = CreateStrictMock<SecretKey>();
    }

    [Fact]
    public void Compare_ExpiresWhen_LeftIsNewer()
    {
        var expiresWhenLeft = DateTimeOffset.UtcNow;
        var expiresWhenRight = expiresWhenLeft.AddDays(-1);

        var metadataLeft = new KeyMetadata { ExpiresWhen = expiresWhenLeft };
        MockSecretKeyLeft.Setup(x => x.Metadata).Returns(metadataLeft).Verifiable();

        var metadataRight = new KeyMetadata { ExpiresWhen = expiresWhenRight };
        MockSecretKeyRight.Setup(x => x.Metadata).Returns(metadataRight).Verifiable();

        var result = SecretKeyExpiresWhenComparer.Singleton.Compare(
            MockSecretKeyLeft.Object,
            MockSecretKeyRight.Object
        );
        Assert.Equal(-1, result);
    }

    [Fact]
    public void Compare_ExpiresWhen_RightIsNewer()
    {
        var expiresWhenLeft = DateTimeOffset.UtcNow;
        var expiresWhenRight = expiresWhenLeft.AddDays(1);

        var metadataLeft = new KeyMetadata { ExpiresWhen = expiresWhenLeft };
        MockSecretKeyLeft.Setup(x => x.Metadata).Returns(metadataLeft).Verifiable();

        var metadataRight = new KeyMetadata { ExpiresWhen = expiresWhenRight };
        MockSecretKeyRight.Setup(x => x.Metadata).Returns(metadataRight).Verifiable();

        var result = SecretKeyExpiresWhenComparer.Singleton.Compare(
            MockSecretKeyLeft.Object,
            MockSecretKeyRight.Object
        );
        Assert.Equal(1, result);
    }
}
