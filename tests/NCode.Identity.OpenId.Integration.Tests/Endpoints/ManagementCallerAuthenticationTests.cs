// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Host;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// A management route takes its caller only from a management access token, whatever principal another authentication scheme set. See ADR-1484.
public class ManagementCallerAuthenticationTests
{
    #region Helpers

    private static WebApplicationFactory<HostApiMarker> WithAnotherDefaultScheme(
        HostApplicationFactory factory
    ) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(serviceCollection =>
                serviceCollection
                    .AddAuthentication(TestSubjectAuthenticationHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestSubjectAuthenticationHandler>(
                        TestSubjectAuthenticationHandler.SchemeName,
                        configureOptions: null
                    )
            )
        );

    #endregion

    #region GetCaller Tests

    [Theory]
    [InlineData("/api/me")]
    [InlineData("/api/me/teams")]
    [InlineData("/api/me/tenants")]
    public async Task GetCaller_WithoutAToken_IsAnUnauthorizedProblem(string path)
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    #endregion

    #region GetManagementRoute Tests

    [Theory]
    [InlineData("/api/capabilities")]
    [InlineData("/api/me")]
    [InlineData("/api/me/teams")]
    [InlineData("/api/me/tenants")]
    [InlineData("/api/teams")]
    public async Task GetManagementRoute_WhenAnotherSchemeAuthenticatesTheRequest_IsAnUnauthorizedProblem(
        string path
    )
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var anotherSchemeFactory = WithAnotherDefaultScheme(factory);
        using var client = anotherSchemeFactory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestSubjectAuthenticationHandler.SubjectHeaderName, "alice");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    #endregion
}
