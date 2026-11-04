// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Persistence.Protection;

public class SecretOwnerTests
{
    #region Constructor Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankOwnerId_Throws(string? ownerId)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new SecretOwner(SecretOwnerTypes.Tenant, ownerId!)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankOwnerType_Throws(string? ownerType)
    {
        Assert.ThrowsAny<ArgumentException>(() => new SecretOwner(ownerType!, "tenant-1"));
    }

    #endregion

    #region ForTenant Tests

    [Fact]
    public void ForTenant_SetsTenantType()
    {
        const string tenantId = "tenant-1";

        var owner = SecretOwner.ForTenant(tenantId);

        Assert.Equal(SecretOwnerTypes.Tenant, owner.OwnerType);
        Assert.Equal(tenantId, owner.OwnerId);
    }

    #endregion

    #region Platform Tests

    [Fact]
    public void Platform_IsThePlatformOwner()
    {
        var owner = SecretOwner.Platform;

        Assert.Equal(SecretOwnerTypes.Platform, owner.OwnerType);
        Assert.Equal("platform", owner.OwnerType);
        Assert.Equal(SecretOwner.PlatformOwnerId, owner.OwnerId);
        Assert.Equal("platform", owner.OwnerId);
    }

    #endregion

    #region Normalize Tests

    [Fact]
    public void Normalize_LowerCasesBoth()
    {
        var owner = new SecretOwner("Tenant", "Tenant-ABC");

        var normalized = owner.Normalize();

        Assert.Equal("tenant", normalized.OwnerType);
        Assert.Equal("tenant-abc", normalized.OwnerId);
    }

    #endregion

    #region Equals Tests

    [Fact]
    public void Equals_SameValues_AreEqual()
    {
        var first = new SecretOwner("tenant", "tenant-1");
        var second = new SecretOwner("tenant", "tenant-1");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_JoinsWithColon()
    {
        var owner = new SecretOwner("tenant", "tenant-1");

        var result = owner.ToString();

        Assert.Equal("tenant:tenant-1", result);
    }

    #endregion
}
