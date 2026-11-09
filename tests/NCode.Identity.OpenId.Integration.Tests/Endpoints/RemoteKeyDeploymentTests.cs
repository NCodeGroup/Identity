// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.RemoteKeys;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Drives a host whose tenants keep their keys in a key management service: every key the host creates is tagged
/// with the deployment and listed under it, and a host whose deployment scope is missing or whose
/// <c>SecretProtection:RemoteKeys</c> section holds an unknown member does not start.
/// </summary>
// See ADR-1478.
public class RemoteKeyDeploymentTests
{
    private const string JwksPath = "/oauth2/jwks";
    private const string DeploymentKey = "SecretProtection:RemoteKeys:Deployment";
    private const string UnknownKey = "SecretProtection:RemoteKeys:Unknown";
    private const string ProviderSettingConfigKey =
        $"OpenId:Platform:Settings:{OpenIdSettingNames.TenantKeyProvider}";

    private static Dictionary<string, string?> CreateStaticConfiguration() =>
        new() { [ProviderSettingConfigKey] = TestRemoteKeyProvider.ProviderName };

    #region ListKeysAsync Tests

    [Fact]
    public async Task ListKeysAsync_AfterProvisioning_ListsEveryCreatedKeyOfTheDeployment()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteKeyApplicationFactory(provider, CreateStaticConfiguration());
        using var client = factory.CreateClient();

        // The first request serves the tenant, which provisions its keys.
        await GetJwksAsync(client);

        var listed = await ListAsync(provider, "integration");
        Assert.NotEmpty(provider.CreatedKeyIds);
        Assert.Equal(
            provider.CreatedKeyIds.Order(StringComparer.Ordinal),
            listed.Select(listing => listing.KeyId).Order(StringComparer.Ordinal)
        );
        Assert.All(
            listed,
            listing => Assert.StartsWith("ncid-", listing.GroupName, StringComparison.Ordinal)
        );

        var other = await ListAsync(provider, "other");
        Assert.Empty(other);
    }

    #endregion

    #region CreateClient Tests

    [Fact]
    public void CreateClient_ProviderWithoutDeployment_HostDoesNotStart()
    {
        var configuration = CreateStaticConfiguration();
        configuration[DeploymentKey] = string.Empty;

        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteKeyApplicationFactory(provider, configuration);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(DeploymentKey, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CreateClient_UnknownRemoteKeysMember_HostDoesNotStart()
    {
        var configuration = CreateStaticConfiguration();
        configuration[UnknownKey] = "x";

        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteKeyApplicationFactory(provider, configuration);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Unknown", exception.ToString(), StringComparison.Ordinal);
    }

    #endregion

    #region Helpers

    private static async Task<List<RemoteKeyListing>> ListAsync(
        TestRemoteKeyProvider provider,
        string deployment
    )
    {
        var listed = new List<RemoteKeyListing>();
        await foreach (
            var listing in provider.ListKeysAsync(
                new RemoteKeyListRequest { Deployment = deployment },
                CancellationToken.None
            )
        )
        {
            listed.Add(listing);
        }

        return listed;
    }

    private static async Task GetJwksAsync(HttpClient client)
    {
        using var response = await client.GetAsync(JwksPath);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.NotEmpty(JsonElement.Parse(body).GetProperty("keys").EnumerateArray());
    }

    #endregion
}
