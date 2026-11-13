// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Setup;
using NCode.Identity.OpenId.Setup.Contracts;
using NCode.Identity.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// An uninitialized instance is set up once through /setup with a one-time token, which creates the root global
// administrator and ends the surface (ADR-1512).
public class InstanceSetupTests
{
    private const string SetupToken = "integration-test-setup-token-0123456789";
    private const string RootUserName = "root-admin";
    private const string RootPassword = "root-admin-password-0123";
    private const string StatusPath = "/setup/api/status";
    private const string RootPath = "/setup/api/root";
    private const string PagePath = "/setup/";

    #region Scaffolding

    private static ConfiguredHostApplicationFactory CreateFactory() =>
        new(new Dictionary<string, string?> { [$"{SetupOptions.SectionName}:Token"] = SetupToken });

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async ValueTask<HttpResponseMessage> PostRootAsync(
        HttpClient client,
        string token,
        CancellationToken cancellationToken
    ) =>
        await client.PostAsJsonAsync(
            RootPath,
            new CreateRootRequest
            {
                Token = token,
                UserName = RootUserName,
                Email = "root-admin@example.test",
                Password = RootPassword,
            },
            cancellationToken
        );

    #endregion

    #region Status

    [Fact]
    public async Task GetStatus_WhenTheInstanceIsUninitialized_ReturnsTheConfiguredTokenSource()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(StatusPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<SetupStatusResponse>();
        Assert.NotNull(status);
        Assert.Equal(SetupTokenSources.Configured, status.TokenSource);
        Assert.Equal(SetupDefaults.MinimumRootPasswordLength, status.MinimumPasswordLength);
    }

    #endregion

    #region Root

    [Fact]
    public async Task PostRoot_WhenTheTokenIsWrong_ReturnsForbiddenAndLeavesTheInstanceUninitialized()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);

        using var response = await PostRootAsync(
            client,
            "a-wrong-setup-token-0123456789012345",
            CancellationToken.None
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var status = await client.GetAsync(StatusPath);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
    }

    [Fact]
    public async Task PostRoot_WhenTheTokenIsRight_ReturnsCreatedAndAssignsGlobalAdminAtThePlatform()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);

        using var response = await PostRootAsync(client, SetupToken, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var root = await response.Content.ReadFromJsonAsync<CreateRootResponse>();
        Assert.NotNull(root);
        Assert.Equal(RootUserName, root.UserName);

        using var scope = factory.Services.CreateScope();
        await using var storeManager = await scope
            .ServiceProvider.GetRequiredService<IStoreManagerFactory>()
            .CreateAsync(CancellationToken.None);
        var assignments = await storeManager
            .GetStore<IRoleAssignmentStore>()
            .GetByPrincipalAsync(root.PrincipalId, CancellationToken.None);

        var assignment = Assert.Single(assignments);
        Assert.Equal(root.TenantId, assignment.TenantId);
        Assert.Equal(BuiltInRoles.GlobalAdmin, assignment.RoleName);
        Assert.Equal(OpenIdResourceTypes.Platform, assignment.ResourceType);
        Assert.Equal(OpenIdConstants.PlatformResourceId, assignment.ResourceId);
    }

    [Fact]
    public async Task PostRoot_WhenTheInstanceIsInitialized_AnswersNotFoundOnEverySetupRoute()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);

        using var created = await PostRootAsync(client, SetupToken, CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var status = await client.GetAsync(StatusPath);
        using var root = await PostRootAsync(client, SetupToken, CancellationToken.None);
        using var page = await client.GetAsync(PagePath);

        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, root.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
    }

    #endregion
}
