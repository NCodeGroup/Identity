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

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Host;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

public class UserInfoEndpointTests
{
    private const string UserInfoPath = "/oauth2/userinfo";

    private static WebApplicationFactory<HostApiMarker> WithTestAuthentication(
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

    [Fact]
    public async Task GetUserInfo_WithoutAuthenticatedSubject_ReturnsUnauthorized()
    {
        using var factory = new HostApplicationFactory();
        using var authFactory = WithTestAuthentication(factory);

        var client = authFactory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.GetAsync(UserInfoPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserInfo_WithAuthenticatedSubject_ReturnsSubClaim()
    {
        using var factory = new HostApplicationFactory();
        using var authFactory = WithTestAuthentication(factory);

        var client = authFactory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoPath);
        request.Headers.Add(TestSubjectAuthenticationHandler.SubjectHeaderName, "alice");

        using var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        Assert.Equal("alice", document.RootElement.GetProperty("sub").GetString());
    }

    [Fact]
    public async Task PostUserInfo_WithAuthenticatedSubject_ReturnsSubClaim()
    {
        using var factory = new HostApplicationFactory();
        using var authFactory = WithTestAuthentication(factory);

        var client = authFactory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var request = new HttpRequestMessage(HttpMethod.Post, UserInfoPath)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>()),
        };
        request.Headers.Add(TestSubjectAuthenticationHandler.SubjectHeaderName, "bob");

        using var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        Assert.Equal("bob", document.RootElement.GetProperty("sub").GetString());
    }

    [Fact]
    public async Task GetUserInfo_WithLocalAccountAndScope_ReturnsAccountSourcedScopeClaims()
    {
        using var factory = new HostApplicationFactory();

        // Seed the local account and its self-issued federated identity (what a real login provisions), then present a
        // subject whose id is the provisioned principal id and whose 'scope' grants profile+email (OpenID Connect
        // Core 1.0 §5.4): the account-sourced enricher must return those claims from UserInfo, not just 'sub'.
        var principalId = await factory.SeedLocalAccountAsync(
            localAccountId: "local-alice",
            email: "alice@localhost",
            emailVerified: true,
            ("given_name", "Alice"),
            ("family_name", "Example")
        );

        using var authFactory = WithTestAuthentication(factory);
        var client = authFactory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoPath);
        request.Headers.Add(TestSubjectAuthenticationHandler.SubjectHeaderName, principalId);
        request.Headers.Add(
            TestSubjectAuthenticationHandler.ScopeHeaderName,
            "openid profile email"
        );

        using var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("email", out var emailProp), body);
        Assert.Equal("alice@localhost", emailProp.GetString());
        Assert.True(root.GetProperty("email_verified").GetBoolean());
        Assert.Equal("Alice", root.GetProperty("given_name").GetString());
        Assert.Equal("Example", root.GetProperty("family_name").GetString());
    }

    [Fact]
    public async Task Discovery_AdvertisesUserInfoEndpoint()
    {
        using var factory = new HostApplicationFactory();

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.GetAsync("/.well-known/openid-configuration");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        Assert.True(
            document.RootElement.TryGetProperty("userinfo_endpoint", out var endpoint),
            body
        );
        Assert.EndsWith(UserInfoPath, endpoint.GetString());
    }
}
