// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

public class DeviceAuthorizationEndpointTests(HostApplicationFactory factory)
    : IClassFixture<HostApplicationFactory>
{
    private HostApplicationFactory Factory { get; } = factory;

    #region PostDeviceAuthorization Tests

    [Fact]
    public async Task PostDeviceAuthorization_WithSeededPublicClient_MarksTheResponseNoStore()
    {
        const string clientId = "it-device-no-store";
        await Factory.SeedPublicClientAsync(clientId, "https://client.example/callback");

        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["client_id"] = clientId, ["scope"] = "openid" }
        );
        using var response = await client.PostAsync("/oauth2/device_authorization", form);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var root = JsonElement.Parse(body);
        Assert.True(root.TryGetProperty("device_code", out _));
        Assert.True(root.TryGetProperty("user_code", out _));

        NoStoreAssert.IsNoStore(response);
    }

    #endregion
}
