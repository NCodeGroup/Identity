// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Persistence.Protection;

public class PlatformOwnerRootKeyPolicyTests
{
    private readonly PlatformOwnerRootKeyPolicy _policy = new();

    [Theory]
    [InlineData("platform")]
    [InlineData("Platform")]
    [InlineData("PLATFORM")]
    public void CanBringRootKey_ThePlatformOwner_IsRefusedWithItsReason(string ownerType)
    {
        var allowed = _policy.CanBringRootKey(
            new SecretOwner(ownerType, SecretOwner.PlatformOwnerId),
            out var reason
        );

        Assert.False(allowed);
        Assert.Equal(
            "The platform's master keys protect the deployment's Data Protection key ring and the host's own data, so they are always wrapped by the deployment's root key.",
            reason
        );
    }

    [Fact]
    public void CanBringRootKey_ATenant_IsAllowed()
    {
        var allowed = _policy.CanBringRootKey(SecretOwner.ForTenant("tenant-a"), out var reason);

        Assert.True(allowed);
        Assert.Null(reason);
    }
}
