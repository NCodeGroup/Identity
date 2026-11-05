// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Persistence.Protection;

public class SecretOwnerComparerTests
{
    #region Equals Tests

    [Fact]
    public void Equals_DifferentCase_ReturnsTrue()
    {
        var first = new SecretOwner("Tenant", "Tenant-ABC");
        var second = new SecretOwner("TENANT", "tenant-abc");

        var result = SecretOwnerComparer.Instance.Equals(first, second);

        Assert.True(result);
    }

    [Fact]
    public void Equals_DifferentOwnerId_ReturnsFalse()
    {
        var first = SecretOwner.ForTenant("tenant-1");
        var second = SecretOwner.ForTenant("tenant-2");

        var result = SecretOwnerComparer.Instance.Equals(first, second);

        Assert.False(result);
    }

    [Fact]
    public void Equals_DifferentOwnerType_ReturnsFalse()
    {
        var first = new SecretOwner(SecretOwnerTypes.Tenant, "owner-1");
        var second = new SecretOwner(SecretOwnerTypes.Platform, "owner-1");

        var result = SecretOwnerComparer.Instance.Equals(first, second);

        Assert.False(result);
    }

    #endregion

    #region GetHashCode Tests

    [Fact]
    public void GetHashCode_DifferentCase_ReturnsSameHashCode()
    {
        var first = new SecretOwner("Tenant", "Tenant-ABC");
        var second = new SecretOwner("TENANT", "tenant-abc");

        var firstHashCode = SecretOwnerComparer.Instance.GetHashCode(first);
        var secondHashCode = SecretOwnerComparer.Instance.GetHashCode(second);

        Assert.Equal(firstHashCode, secondHashCode);
    }

    #endregion
}
