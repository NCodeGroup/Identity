// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class BootstrapAdminHostManagedClientPolicyTests : BaseTests
{
    private const string BootstrapClientId = "bootstrap-admin";
    private const string BootstrapTenantId = "default";

    private static BootstrapAdminHostManagedClientPolicy CreatePolicy(
        BootstrapAdminOptions options
    ) => new(Options.Create(options));

    private static BootstrapAdminOptions ConfiguredOptions() =>
        new()
        {
            ClientId = BootstrapClientId,
            ClientSecret = "secret",
            TenantId = BootstrapTenantId,
        };

    #region IsHostManaged Tests

    [Fact]
    public void IsHostManaged_WhenNoClientIsConfigured_ReturnsFalse()
    {
        var policy = CreatePolicy(new BootstrapAdminOptions());

        Assert.False(policy.IsHostManaged(BootstrapTenantId, BootstrapClientId));
    }

    [Fact]
    public void IsHostManaged_WhenTheClientIsTheConfiguredOneInTheConfiguredTenant_ReturnsTrue()
    {
        var policy = CreatePolicy(ConfiguredOptions());

        Assert.True(policy.IsHostManaged(BootstrapTenantId, BootstrapClientId));
    }

    [Fact]
    public void IsHostManaged_WhenTheSameClientIdIsInAnotherTenant_ReturnsFalse()
    {
        var policy = CreatePolicy(ConfiguredOptions());

        Assert.False(policy.IsHostManaged("acme", BootstrapClientId));
    }

    [Fact]
    public void IsHostManaged_WhenTheClientIsAnotherClientInTheConfiguredTenant_ReturnsFalse()
    {
        var policy = CreatePolicy(ConfiguredOptions());

        Assert.False(policy.IsHostManaged(BootstrapTenantId, "some-other-client"));
    }

    #endregion
}
