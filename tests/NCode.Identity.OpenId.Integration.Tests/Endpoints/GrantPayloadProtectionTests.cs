// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// A persisted grant carries tokens, PII and request parameters, so its payload is stored only in protected form (ADR-1235).
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
        await Factory.SeedPublicClientAsync(ClientId, [RedirectUri], CancellationToken.None);

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
