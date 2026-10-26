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
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

// A persisted grant carries tokens, PII and request parameters, so its payload is stored only in protected form (ADR-0265).
public class GrantPayloadProtectionTests(HostApplicationFactory factory)
    : IClassFixture<HostApplicationFactory>
{
    private const string ClientId = "it-grant-payload-client";
    private const string RedirectUri = "https://client.example/callback";
    private const string Marker = "grant-payload-marker-7f3c9a";

    private HostApplicationFactory Factory { get; } = factory;

    [Fact]
    public async Task PostPar_StoresTheRequestOnlyInProtectedForm()
    {
        await Factory.SeedPublicClientAsync(ClientId, RedirectUri);

        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var values = new List<KeyValuePair<string, string>>
        {
            new("client_id", ClientId),
            new("redirect_uri", RedirectUri),
            new("response_type", "code"),
            new("scope", "openid"),
            new("state", Marker),
        };
        values.AddRange(TestPkce.FormFields);
        using var form = new FormUrlEncodedContent(values);
        using var response = await client.PostAsync("/oauth2/par", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var store = storeManager.GetStore<IGrantStore>();

        var page = await store.GetPageAsync(
            new GrantFilter { ClientId = ClientId },
            new PageRequest { Limit = 100 },
            CancellationToken.None
        );

        Assert.NotEmpty(page.Items);
        foreach (var item in page.Items)
        {
            Assert.DoesNotContain(Marker, JsonSerializer.Serialize(item));
        }
    }
}
