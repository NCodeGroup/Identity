#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
}
