// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence.Protection;
using Xunit;

namespace NCode.Identity.OpenId.Core.SecretProtection;

public sealed class ControlPlaneTenantRootKeyPolicyTests : BaseTests
{
    private Mock<ITenantResolver> TenantResolver { get; }

    public ControlPlaneTenantRootKeyPolicyTests()
    {
        TenantResolver = CreateStrictMock<ITenantResolver>();
    }

    private ControlPlaneTenantRootKeyPolicy CreatePolicy(string controlPlaneTenantId)
    {
        TenantResolver
            .Setup(x => x.GetControlPlaneTenantId())
            .Returns(controlPlaneTenantId)
            .Verifiable();

        return new ControlPlaneTenantRootKeyPolicy(TenantResolver.Object);
    }

    [Fact]
    public void CanBringRootKey_TheControlPlaneTenant_IsRefusedWithItsReason()
    {
        var policy = CreatePolicy("control-plane");

        var allowed = policy.CanBringRootKey(
            SecretOwner.ForTenant("control-plane"),
            out var reason
        );

        Assert.False(allowed);
        Assert.Equal(
            "The management API authenticates against the control-plane tenant, so a revoked root key would lock out the administrators who restore it. Its keys stay under the deployment's root key.",
            reason
        );
    }

    [Fact]
    public void CanBringRootKey_TheNormalizedControlPlaneTenant_IsRefused()
    {
        var policy = CreatePolicy("Control-Plane");

        var allowed = policy.CanBringRootKey(
            SecretOwner.ForTenant("Control-Plane").Normalize(),
            out var reason
        );

        Assert.False(allowed);
        Assert.NotNull(reason);
    }

    [Fact]
    public void CanBringRootKey_AnotherTenant_IsAllowed()
    {
        var policy = CreatePolicy("control-plane");

        var allowed = policy.CanBringRootKey(SecretOwner.ForTenant("tenant-a"), out var reason);

        Assert.True(allowed);
        Assert.Null(reason);
    }

    [Fact]
    public void CanBringRootKey_AnOwnerOfAnotherTypeWithTheControlPlaneId_IsAllowed()
    {
        var policy = CreatePolicy("control-plane");

        var allowed = policy.CanBringRootKey(
            new SecretOwner("other", "control-plane"),
            out var reason
        );

        Assert.True(allowed);
        Assert.Null(reason);
    }
}
