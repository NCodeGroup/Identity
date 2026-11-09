// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// A management request that fails to bind, or that no route answers, is answered as a problem document, so a caller
// reads one error shape across the management API. See ADR-1483.
public class ManagementRequestBindingTests
{
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";
    private const string ProblemMediaType = "application/problem+json";

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatusCode
    )
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expectedStatusCode, body);
        Assert.Equal(ProblemMediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            (int)expectedStatusCode,
            JsonElement.Parse(body).GetProperty("status").GetInt32()
        );
    }

    #region Binding Tests

    [Fact]
    public async Task CreateScope_WithABodyThatIsNotJson_IsABadRequestProblem()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var administrator = await factory.CreateAdministratorClientAsync();
        using var content = new StringContent("{", SecureEncoding.UTF8, "application/json");

        using var response = await administrator.PostAsync(
            $"{TenantApiPath}/resource-servers/any/scopes",
            content
        );

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateScope_WithAnEmptyBody_IsABadRequestProblem()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var administrator = await factory.CreateAdministratorClientAsync();
        using var content = new StringContent(
            string.Empty,
            SecureEncoding.UTF8,
            "application/json"
        );

        using var response = await administrator.PostAsync(
            $"{TenantApiPath}/resource-servers/any/scopes",
            content
        );

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateScope_WithAContentTypeThatIsNotJson_IsAnUnsupportedMediaTypeProblem()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var administrator = await factory.CreateAdministratorClientAsync();
        using var content = new StringContent("{}", SecureEncoding.UTF8, "text/plain");

        using var response = await administrator.PostAsync(
            $"{TenantApiPath}/resource-servers/any/scopes",
            content
        );

        await AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task ListGrants_WithALimitThatIsNotANumber_IsABadRequestProblem()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var administrator = await factory.CreateAdministratorClientAsync();

        using var response = await administrator.GetAsync($"{TenantApiPath}/grants?limit=abc");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListGrants_WithALimitThatIsNotANumberAndNoToken_IsABadRequestProblem()
    {
        using var factory = new HostApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{TenantApiPath}/grants?limit=abc");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListGrants_WithALimitThatIsNotANumberWhenBindingDoesNotThrow_IsABadRequestProblem()
    {
        using var factory = new HostApplicationFactory();
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.PostConfigure<RouteHandlerOptions>(options =>
                    options.ThrowOnBadRequest = false
                )
            )
        );
        using var client = host.CreateClient();

        using var response = await client.GetAsync($"{TenantApiPath}/grants?limit=abc");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    #endregion

    #region Router Tests

    [Fact]
    public async Task ReplaceGrants_WithAMethodNoRouteAllows_IsAMethodNotAllowedProblem()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var administrator = await factory.CreateAdministratorClientAsync();
        using var content = new StringContent("{}", SecureEncoding.UTF8, "application/json");

        using var response = await administrator.PutAsync($"{TenantApiPath}/grants", content);

        await AssertProblemAsync(response, HttpStatusCode.MethodNotAllowed);
        Assert.NotEmpty(response.Content.Headers.Allow);
    }

    [Fact]
    public async Task GetUnknownRoute_UnderTheManagementPrefix_IsANotFoundProblem()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var administrator = await factory.CreateAdministratorClientAsync();

        using var response = await administrator.GetAsync("/api/no-such-route");

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUnknownRoute_OutsideTheManagementPrefix_IsNotAProblem()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var administrator = await factory.CreateAdministratorClientAsync();

        using var response = await administrator.GetAsync("/no-such-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual(ProblemMediaType, response.Content.Headers.ContentType?.MediaType);
    }

    #endregion
}
