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
        var second = new SecretOwner(SecretOwnerTypes.Server, "owner-1");

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
