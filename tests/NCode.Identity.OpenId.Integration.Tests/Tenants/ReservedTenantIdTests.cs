// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff;
using NCode.Identity.OpenId.Setup;
using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Tenants;

/// <summary>
/// Provides tests proving that each top-level route prefix the host maps is a reserved tenant identifier.
/// </summary>
public class ReservedTenantIdTests
{
    #region IsValidTenantId Tests

    [Theory]
    [MemberData(nameof(MappedRoutePrefixes))]
    public void IsValidTenantId_WhenMappedRoutePrefix_ReturnsFalse(string prefix)
    {
        Assert.False(TenantAddressRules.IsValidTenantId(prefix));
    }

    public static TheoryData<string> MappedRoutePrefixes() =>
        new() { SetupDefaults.PathPrefix.Trim('/'), BffDefaults.EndpointPrefix.Trim('/') };

    #endregion
}
