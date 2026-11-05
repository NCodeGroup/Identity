// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Persistence.Protection;

public class ServerOwnerRootKeyPolicyTests
{
    private readonly ServerOwnerRootKeyPolicy _policy = new();

    [Theory]
    [InlineData("server")]
    [InlineData("Server")]
    [InlineData("SERVER")]
    public void CanBringRootKey_TheServerOwner_IsRefusedWithItsReason(string ownerType)
    {
        var allowed = _policy.CanBringRootKey(
            new SecretOwner(ownerType, "server-1"),
            out var reason
        );

        Assert.False(allowed);
        Assert.Equal(
            "The server's master keys protect the deployment's own Data Protection key ring, so they are always wrapped by the deployment's root key.",
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
