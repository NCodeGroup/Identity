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
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

public class AuthorizationEndpointSeededClientTests(PlaygroundApplicationFactory factory)
    : IClassFixture<PlaygroundApplicationFactory>
{
    private const string ClientId = "it-authorize-client";
    private const string RedirectUri = "https://client.example/callback";

    private PlaygroundApplicationFactory Factory { get; } = factory;

    [Fact]
    public async Task GetAuthorize_WithSeededClient_ResolvesClientAndProcessesRequest()
    {
        await Factory.SeedPublicClientAsync(ClientId, RedirectUri);

        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var query =
            $"?client_id={ClientId}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope=openid"
            + "&state=abc123";

        using var response = await client.GetAsync("/oauth2/authorize" + query);

        // The seeded client resolves and the request is loaded/validated; with no authenticated
        // end-user the authorization handler issues an interactive challenge (a redirect), rather
        // than the missing-client 400 returned when no client can be resolved.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
