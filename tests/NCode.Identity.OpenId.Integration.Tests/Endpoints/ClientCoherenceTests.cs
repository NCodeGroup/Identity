// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// A change to a tenant's settings is checked against the clients the tenant already stores: a redirect URI the new
// settings would not permit refuses the change, including when selecting a security profile narrows the permitted
// redirect URI kinds (ADR-1206).
public class ClientCoherenceTests
{
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";
    private const string RedirectUri = "http://client.example/callback";

    #region PatchTenantSettings Tests

    [Fact]
    public async Task PatchTenantSettings_WhenTheKindsNoLongerPermitAClientsRedirectUri_ReturnsValidationProblem()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        var clientId = await CreateClientAsync(administrator, CancellationToken.None);

        var (statusCode, body) = await PatchTenantSettingAsync(
            administrator,
            OpenIdSettingNames.RedirectUriKindsSupported,
            [OpenIdConstants.RedirectUriKinds.Https, OpenIdConstants.RedirectUriKinds.Loopback],
            CancellationToken.None
        );

        Assert.True(statusCode == HttpStatusCode.BadRequest, body);
        Assert.True(HasError(body, OpenIdSettingNames.RedirectUriKindsSupported), body);
        Assert.Contains(clientId, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PatchTenantSettings_WhenTheKindsStillPermitEveryRedirectUri_ReturnsNoContent()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateClientAsync(administrator, CancellationToken.None);

        var (statusCode, body) = await PatchTenantSettingAsync(
            administrator,
            OpenIdSettingNames.RedirectUriKindsSupported,
            [
                OpenIdConstants.RedirectUriKinds.Https,
                OpenIdConstants.RedirectUriKinds.Loopback,
                OpenIdConstants.RedirectUriKinds.Http,
                OpenIdConstants.RedirectUriKinds.PrivateUse,
            ],
            CancellationToken.None
        );

        Assert.True(statusCode == HttpStatusCode.NoContent, body);
    }

    [Fact]
    public async Task PatchTenantSettings_WhenTheTenantSelectsFapi1Advanced_ReportsTheClientsRedirectUri()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        var clientId = await CreateClientAsync(administrator, CancellationToken.None);

        var (statusCode, body) = await PatchTenantSettingAsync(
            administrator,
            OpenIdSettingNames.SecurityProfiles,
            [OpenIdConstants.SecurityProfiles.Fapi1Advanced],
            CancellationToken.None
        );

        Assert.True(statusCode == HttpStatusCode.BadRequest, body);
        Assert.True(HasError(body, OpenIdSettingNames.RedirectUriKindsSupported), body);
        Assert.Contains(clientId, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PatchTenantSettings_WhenItLowersAClientsAccessTokenLifetime_ReturnsValidationProblem()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        var (createStatusCode, createBody) = await PostClientWithSettingsAsync(
            administrator,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.AccessTokenLifetime] = "00:04:00",
            },
            CancellationToken.None
        );
        Assert.True(createStatusCode == HttpStatusCode.Created, createBody);

        var (statusCode, body) = await PatchTenantSettingValueAsync(
            administrator,
            OpenIdSettingNames.AccessTokenLifetime,
            "00:03:00",
            CancellationToken.None
        );

        Assert.True(statusCode == HttpStatusCode.BadRequest, body);
        Assert.True(HasError(body, OpenIdSettingNames.AccessTokenLifetime), body);
        Assert.Contains("'00:04:00'", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PatchTenantSettings_WhenItRaisesTheCeilingOfAnAlreadyLoweredClientValue_ReturnsNoContent()
    {
        const string LoweredClientId = "it-lowered-client";
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        var (lowerStatusCode, lowerBody) = await PatchTenantSettingValueAsync(
            administrator,
            OpenIdSettingNames.AccessTokenLifetime,
            "00:03:00",
            CancellationToken.None
        );
        Assert.True(lowerStatusCode == HttpStatusCode.NoContent, lowerBody);

        // A direct store write, so no check refuses the client: the tenant's three-minute ceiling already lowers its
        // one-hour lifetime.
        await factory.SeedPublicClientWithSettingsAsync(
            LoweredClientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.AccessTokenLifetime] = "01:00:00",
            },
            [RedirectUri],
            CancellationToken.None
        );

        var (statusCode, body) = await PatchTenantSettingValueAsync(
            administrator,
            OpenIdSettingNames.AccessTokenLifetime,
            "00:04:00",
            CancellationToken.None
        );

        Assert.True(statusCode == HttpStatusCode.NoContent, body);
    }

    #endregion

    #region PostClient Tests

    [Fact]
    public async Task PostClient_WhenItListsAGrantTypeItsTenantExcludes_ReturnsValidationProblem()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        var (statusCode, body) = await PostClientAsync(
            administrator,
            [OpenIdProtocol.GrantTypes.AuthorizationCode, OpenIdProtocol.GrantTypes.Implicit],
            CancellationToken.None
        );

        Assert.True(statusCode == HttpStatusCode.BadRequest, body);
        Assert.True(HasError(body, OpenIdSettingNames.GrantTypesSupported), body);
        Assert.Contains($"'{OpenIdProtocol.GrantTypes.Implicit}'", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostClient_WhenItsAccessTokenLifetimeExceedsItsTenants_ReturnsValidationProblem()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        var (statusCode, body) = await PostClientWithSettingsAsync(
            administrator,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.AccessTokenLifetime] = "01:00:00",
            },
            CancellationToken.None
        );

        Assert.True(statusCode == HttpStatusCode.BadRequest, body);
        Assert.True(HasError(body, OpenIdSettingNames.AccessTokenLifetime), body);
        Assert.Contains("'00:05:00'", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostClient_WhenItListsOnlyPermittedGrantTypes_ReturnsCreated()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        var (statusCode, body) = await PostClientAsync(
            administrator,
            [OpenIdProtocol.GrantTypes.AuthorizationCode, OpenIdProtocol.GrantTypes.RefreshToken],
            CancellationToken.None
        );

        Assert.True(statusCode == HttpStatusCode.Created, body);
    }

    #endregion

    #region Scaffolding

    private static BootstrapAdminApplicationFactory CreateFactory() => new();

    private static async ValueTask<string> CreateClientAsync(
        HttpClient administrator,
        CancellationToken cancellationToken
    )
    {
        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients",
            new
            {
                isDisabled = false,
                settings = new Dictionary<string, object>
                {
                    ["redirect_uris"] = new[] { RedirectUri },
                    ["token_endpoint_auth_method"] = OpenIdProtocol
                        .ClientAuthenticationMethods
                        .ClientSecretPost,
                },
            },
            cancellationToken
        );
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var jsonElement = JsonElement.Parse(body);
        return jsonElement.GetProperty("clientId").GetString()!;
    }

    private static ValueTask<(HttpStatusCode StatusCode, string Body)> PostClientAsync(
        HttpClient administrator,
        string[] grantTypes,
        CancellationToken cancellationToken
    ) =>
        PostClientWithSettingsAsync(
            administrator,
            new Dictionary<string, object> { ["grant_types_supported"] = grantTypes },
            cancellationToken
        );

    private static async ValueTask<(
        HttpStatusCode StatusCode,
        string Body
    )> PostClientWithSettingsAsync(
        HttpClient administrator,
        Dictionary<string, object> settings,
        CancellationToken cancellationToken
    )
    {
        var clientSettings = new Dictionary<string, object>(settings)
        {
            ["redirect_uris"] = new[] { RedirectUri },
            ["token_endpoint_auth_method"] = OpenIdProtocol
                .ClientAuthenticationMethods
                .ClientSecretPost,
        };

        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients",
            new { isDisabled = false, settings = clientSettings },
            cancellationToken
        );
        return (response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private static ValueTask<(HttpStatusCode StatusCode, string Body)> PatchTenantSettingAsync(
        HttpClient administrator,
        string settingName,
        string[] values,
        CancellationToken cancellationToken
    ) => PatchTenantSettingValueAsync(administrator, settingName, values, cancellationToken);

    private static async ValueTask<(
        HttpStatusCode StatusCode,
        string Body
    )> PatchTenantSettingValueAsync(
        HttpClient administrator,
        string settingName,
        object value,
        CancellationToken cancellationToken
    )
    {
        var operations = new[]
        {
            new
            {
                op = "add",
                path = $"/{settingName}",
                value = value,
            },
        };
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{TenantApiPath}/settings")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(operations),
                SecureEncoding.UTF8,
                "application/json-patch+json"
            ),
        };

        using var response = await administrator.SendAsync(request, cancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private static bool HasError(string body, string settingName)
    {
        var jsonElement = JsonElement.Parse(body);
        return jsonElement.TryGetProperty("errors", out var errors)
            && errors.TryGetProperty(settingName, out _);
    }

    #endregion
}
