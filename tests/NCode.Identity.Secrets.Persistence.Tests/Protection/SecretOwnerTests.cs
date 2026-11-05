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

    #region TenantId Tests

    [Theory]
    [InlineData("tenant", "tenant-1", "tenant-1")]
    [InlineData("Tenant", "Tenant-1", "Tenant-1")]
    [InlineData("server", "server-1", null)]
    public void TenantId_ByOwnerType_IsTheOwnerIdOfATenant(
        string ownerType,
        string ownerId,
        string? expected
    )
    {
        var owner = new SecretOwner(ownerType, ownerId);

        Assert.Equal(expected, owner.TenantId);
    }

    [Fact]
    public void TenantId_DefaultOwner_IsNull()
    {
        Assert.Null(default(SecretOwner).TenantId);
    }

    #endregion

    #region ForServer Tests

    [Fact]
    public void ForServer_SetsServerType()
    {
        const string serverId = "server-1";

        var owner = SecretOwner.ForServer(serverId);

        Assert.Equal(SecretOwnerTypes.Server, owner.OwnerType);
        Assert.Equal(serverId, owner.OwnerId);
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

    #region TryParse Tests

    [Theory]
    [InlineData("tenant:tenant-1", "tenant", "tenant-1")]
    [InlineData("server:a:b", "server", "a:b")]
    public void TryParse_ValidText_ReturnsOwner(string text, string ownerType, string ownerId)
    {
        Assert.True(SecretOwner.TryParse(text, out var owner));
        Assert.Equal(ownerType, owner.OwnerType);
        Assert.Equal(ownerId, owner.OwnerId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tenant")]
    [InlineData(":id")]
    [InlineData("tenant:")]
    [InlineData(" :id")]
    [InlineData("tenant:  ")]
    public void TryParse_InvalidText_ReturnsFalse(string? text)
    {
        Assert.False(SecretOwner.TryParse(text, out var owner));
        Assert.Equal(default, owner);
    }

    [Fact]
    public void TryParse_RoundTripsToString()
    {
        var owner = SecretOwner.ForTenant("tenant-1");

        Assert.True(SecretOwner.TryParse(owner.ToString(), out var parsed));
        Assert.Equal(owner, parsed);
    }

    #endregion
}
