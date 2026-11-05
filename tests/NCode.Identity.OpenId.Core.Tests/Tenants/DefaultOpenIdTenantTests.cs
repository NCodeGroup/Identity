// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Disposables;
using NCode.Identity.Models;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Settings;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class DefaultOpenIdTenantTests
{
    private static async ValueTask<(
        DefaultOpenIdTenant Tenant,
        Func<int> TeamReleases
    )> CreateTenantAsync()
    {
        var teamReleases = 0;

        // Only the tenant holds the team once the creator releases its own lease.
        await using var settings = AsyncSharedReference.Create(
            Mock.Of<IReadOnlySettingCollectionProvider>(),
            _ => ValueTask.CompletedTask
        );
        await using var secrets = AsyncSharedReference.Create(
            Mock.Of<ISecretKeyCollectionProvider>(),
            _ => ValueTask.CompletedTask
        );
        await using var team = AsyncSharedReference.Create(
            Mock.Of<OpenIdTeam>(),
            _ =>
            {
                Interlocked.Increment(ref teamReleases);
                return ValueTask.CompletedTask;
            }
        );

        var tenant = new DefaultOpenIdTenant(
            "acme",
            "platform",
            "Acme",
            "https://op.example/acme",
            mtlsEndpointAliasAuthority: null,
            new UriDescriptor
            {
                Scheme = "https",
                Host = new HostString("op.example"),
                Path = "/acme",
            },
            settings,
            secrets,
            team.AddReference(),
            PropertyBagFactory.Create()
        );

        return (tenant, () => Volatile.Read(ref teamReleases));
    }

    #region Team Tests

    [Fact]
    public async Task Team_ReturnsTheTeamTheTenantHolds()
    {
        var teamInstance = Mock.Of<OpenIdTeam>();

        await using var settings = AsyncSharedReference.Create(
            Mock.Of<IReadOnlySettingCollectionProvider>(),
            _ => ValueTask.CompletedTask
        );
        await using var secrets = AsyncSharedReference.Create(
            Mock.Of<ISecretKeyCollectionProvider>(),
            _ => ValueTask.CompletedTask
        );
        await using var team = AsyncSharedReference.Create(
            teamInstance,
            _ => ValueTask.CompletedTask
        );

        await using var tenant = new DefaultOpenIdTenant(
            "acme",
            "platform",
            "Acme",
            "https://op.example/acme",
            mtlsEndpointAliasAuthority: null,
            new UriDescriptor
            {
                Scheme = "https",
                Host = new HostString("op.example"),
                Path = "/acme",
            },
            settings,
            secrets,
            team.AddReference(),
            PropertyBagFactory.Create()
        );

        Assert.Same(teamInstance, tenant.Team);
    }

    #endregion

    #region DisposeAsync Tests

    [Fact]
    public async Task DisposeAsync_ReleasesTheTeamOnce()
    {
        var (tenant, teamReleases) = await CreateTenantAsync();
        Assert.Equal(0, teamReleases());

        await tenant.DisposeAsync();

        Assert.Equal(1, teamReleases());
    }

    [Fact]
    public async Task DisposeAsync_Twice_ReleasesTheTeamOnce()
    {
        var (tenant, teamReleases) = await CreateTenantAsync();

        await tenant.DisposeAsync();
        await tenant.DisposeAsync();

        Assert.Equal(1, teamReleases());
    }

    #endregion
}
