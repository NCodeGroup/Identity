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

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

// A change to a tenant's settings is checked against the clients the tenant already stores: a redirect URI the new
// settings would not permit refuses the change, including when selecting a security profile narrows the permitted
// redirect URI kinds (ADR-0206).
public class ClientCoherenceTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";
    private const string RedirectUri = "http://client.example/callback";

    #region PatchTenantSettings Tests

    [Fact]
    public async Task PatchTenantSettings_WhenTheKindsNoLongerPermitAClientsRedirectUri_ReturnsValidationProblem()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        var clientId = await CreateClientAsync(administrator);

        var (statusCode, body) = await PatchTenantSettingAsync(
            administrator,
            OpenIdSettingNames.RedirectUriKindsSupported,
            [OpenIdConstants.RedirectUriKinds.Https, OpenIdConstants.RedirectUriKinds.Loopback]
        );

        Assert.True(statusCode == HttpStatusCode.BadRequest, body);
        Assert.True(HasError(body, OpenIdSettingNames.RedirectUriKindsSupported), body);
        Assert.Contains(clientId, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PatchTenantSettings_WhenTheKindsStillPermitEveryRedirectUri_ReturnsNoContent()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateClientAsync(administrator);

        var (statusCode, body) = await PatchTenantSettingAsync(
            administrator,
            OpenIdSettingNames.RedirectUriKindsSupported,
            [
                OpenIdConstants.RedirectUriKinds.Https,
                OpenIdConstants.RedirectUriKinds.Loopback,
                OpenIdConstants.RedirectUriKinds.Http,
                OpenIdConstants.RedirectUriKinds.PrivateUse,
            ]
        );

        Assert.True(statusCode == HttpStatusCode.NoContent, body);
    }

    [Fact]
    public async Task PatchTenantSettings_WhenTheTenantSelectsFapi1Advanced_ReportsTheClientsRedirectUri()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        var clientId = await CreateClientAsync(administrator);

        var (statusCode, body) = await PatchTenantSettingAsync(
            administrator,
            OpenIdSettingNames.SecurityProfiles,
            [OpenIdConstants.SecurityProfiles.Fapi1Advanced]
        );

        Assert.True(statusCode == HttpStatusCode.BadRequest, body);
        Assert.True(HasError(body, OpenIdSettingNames.RedirectUriKindsSupported), body);
        Assert.Contains(clientId, body, StringComparison.Ordinal);
    }

    #endregion

    #region PostClient Tests

    [Fact]
    public async Task PostClient_WhenItListsAGrantTypeItsTenantExcludes_ReturnsValidationProblem()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);

        var (statusCode, body) = await PostClientAsync(
            administrator,
            [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit]
        );

        Assert.True(statusCode == HttpStatusCode.BadRequest, body);
        Assert.True(HasError(body, OpenIdSettingNames.GrantTypesSupported), body);
        Assert.Contains($"'{OpenIdConstants.GrantTypes.Implicit}'", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostClient_WhenItListsOnlyPermittedGrantTypes_ReturnsCreated()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);

        var (statusCode, body) = await PostClientAsync(
            administrator,
            [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.RefreshToken]
        );

        Assert.True(statusCode == HttpStatusCode.Created, body);
    }

    #endregion

    #region Scaffolding

    private static ConfiguredHostApplicationFactory CreateFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["BootstrapAdmin:ClientId"] = BootstrapClientId,
                ["BootstrapAdmin:ClientSecret"] = Base64Url.EncodeToString(
                    Encoding.UTF8.GetBytes(BootstrapClientSecret)
                ),
                ["BootstrapAdmin:TenantId"] = HostApplicationFactory.TenantId,
            }
        );

    private static Task<HttpClient> CreateAdministratorClientAsync(
        HostApplicationFactory factory
    ) => factory.CreateManagementClientAsync(BootstrapClientId, BootstrapClientSecret);

    private static async Task<string> CreateClientAsync(HttpClient administrator)
    {
        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients",
            new
            {
                isDisabled = false,
                settings = new Dictionary<string, object>
                {
                    ["redirect_uris"] = new[] { RedirectUri },
                    ["token_endpoint_auth_method"] = OpenIdConstants
                        .ClientAuthenticationMethods
                        .ClientSecretPost,
                },
            }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("clientId").GetString()!;
    }

    private static async Task<(HttpStatusCode StatusCode, string Body)> PostClientAsync(
        HttpClient administrator,
        string[] grantTypes
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
                    ["token_endpoint_auth_method"] = OpenIdConstants
                        .ClientAuthenticationMethods
                        .ClientSecretPost,
                    ["grant_types_supported"] = grantTypes,
                },
            }
        );
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<(HttpStatusCode StatusCode, string Body)> PatchTenantSettingAsync(
        HttpClient administrator,
        string settingName,
        string[] values
    )
    {
        var operations = new[]
        {
            new
            {
                op = "add",
                path = $"/{settingName}",
                value = values,
            },
        };
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{TenantApiPath}/settings")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(operations),
                Encoding.UTF8,
                "application/json-patch+json"
            ),
        };

        using var response = await administrator.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static bool HasError(string body, string settingName)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("errors", out var errors)
            && errors.TryGetProperty(settingName, out _);
    }

    #endregion
}
