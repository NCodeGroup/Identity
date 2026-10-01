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
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Playground;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

public class UserInfoEndpointTests
{
    private const string UserInfoPath = "/oauth2/userinfo";

    private static WebApplicationFactory<PlaygroundApiMarker> WithTestAuthentication(
        PlaygroundApplicationFactory factory
    ) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services
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
        using var factory = new PlaygroundApplicationFactory();
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
        using var factory = new PlaygroundApplicationFactory();
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
        using var factory = new PlaygroundApplicationFactory();
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
    public async Task Discovery_AdvertisesUserInfoEndpoint()
    {
        using var factory = new PlaygroundApplicationFactory();

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
