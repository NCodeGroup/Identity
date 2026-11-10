// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// Rotating a client secret through the management API adds the replacement beside the rotated secret, which keeps
// authenticating until it expires or is deleted (ADR-1320).
public class ClientSecretRotationTests
{
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";

    [Fact]
    public async Task PostRotate_WhenSecretIsShared_KeepsTheRotatedSecretUntilItIsDeleted()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        var client = CreateHttpClient(factory);

        var clientId = await CreateClientAsync(administrator);
        var (secretId, secretMaterial) = await CreateSecretAsync(administrator, clientId);
        var authenticationError = await AuthenticateAsync(client, clientId, secretMaterial);
        Assert.Null(authenticationError);

        var (replacementId, replacementMaterial) = await RotateAsync(
            administrator,
            clientId,
            secretId,
            DateTimeOffset.UtcNow.AddDays(7)
        );
        Assert.NotEqual(secretId, replacementId);
        Assert.False(string.IsNullOrEmpty(replacementMaterial));

        // Both authenticate while the holder switches.
        var replacementError = await AuthenticateAsync(client, clientId, replacementMaterial);
        Assert.Null(replacementError);
        var rotatedSecretError = await AuthenticateAsync(client, clientId, secretMaterial);
        Assert.Null(rotatedSecretError);

        // Both are listed without their material.
        using var listResponse = await administrator.GetAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets"
        );
        var listBody = await listResponse.Content.ReadAsStringAsync();
        Assert.True(listResponse.StatusCode == HttpStatusCode.OK, listBody);
        var list = JsonElement.Parse(listBody);
        var secrets = list.GetProperty("secrets").EnumerateArray().ToList();
        Assert.Equal(2, secrets.Count);
        Assert.All(secrets, secret => Assert.False(secret.TryGetProperty("secretMaterial", out _)));

        // Deleting the rotated secret ends the overlap.
        using var deleteResponse = await administrator.DeleteAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets/{secretId}"
        );
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var deletedSecretError = await AuthenticateAsync(client, clientId, secretMaterial);
        Assert.Equal("invalid_client", deletedSecretError);
        var retainedSecretError = await AuthenticateAsync(client, clientId, replacementMaterial);
        Assert.Null(retainedSecretError);
    }

    [Fact]
    public async Task PostRotate_WhenTheRotatedSecretExpiresAtOnce_OnlyTheReplacementAuthenticates()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        var client = CreateHttpClient(factory);

        var clientId = await CreateClientAsync(administrator);
        var (secretId, secretMaterial) = await CreateSecretAsync(administrator, clientId);

        var (_, replacementMaterial) = await RotateAsync(
            administrator,
            clientId,
            secretId,
            DateTimeOffset.UtcNow.AddMinutes(-1)
        );

        var expiredSecretError = await AuthenticateAsync(client, clientId, secretMaterial);
        Assert.Equal("invalid_client", expiredSecretError);
        var replacementError = await AuthenticateAsync(client, clientId, replacementMaterial);
        Assert.Null(replacementError);
    }

    [Fact]
    public async Task PostRotate_WhenSecretIsUnknown_ReturnsNotFound()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        var clientId = await CreateClientAsync(administrator);

        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets/unknown-secret/rotate",
            new { expiresWhen = DateTimeOffset.UtcNow.AddYears(1) }
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #region Scaffolding

    private static HttpClient CreateHttpClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<string> CreateClientAsync(HttpClient administrator)
    {
        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients",
            new
            {
                isDisabled = false,
                settings = new Dictionary<string, object>
                {
                    ["token_endpoint_auth_method"] = OpenIdProtocol
                        .ClientAuthenticationMethods
                        .ClientSecretPost,
                },
            }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var jsonElement = JsonElement.Parse(body);
        return GetRequiredString(jsonElement, "clientId");
    }

    private static async Task<(string SecretId, string SecretMaterial)> CreateSecretAsync(
        HttpClient administrator,
        string clientId
    )
    {
        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets",
            new
            {
                secretType = "symmetric",
                keySizeBits = 256,
                expiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var jsonElement = JsonElement.Parse(body);
        var createdSecretId = GetRequiredString(jsonElement, "secretId");
        var createdSecretMaterial = GetRequiredString(jsonElement, "secretMaterial");
        return (createdSecretId, createdSecretMaterial);
    }

    // Rotates the secret, giving the replacement a year and the rotated secret the specified grace expiry.
    private static async Task<(string SecretId, string SecretMaterial)> RotateAsync(
        HttpClient administrator,
        string clientId,
        string secretId,
        DateTimeOffset rotatedExpiresWhen
    )
    {
        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets/{secretId}/rotate",
            new { expiresWhen = DateTimeOffset.UtcNow.AddYears(1), rotatedExpiresWhen }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var jsonElement = JsonElement.Parse(body);
        Assert.Equal("symmetric", jsonElement.GetProperty("secretType").GetString());
        var createdSecretId = GetRequiredString(jsonElement, "secretId");
        var createdSecretMaterial = GetRequiredString(jsonElement, "secretMaterial");
        return (createdSecretId, createdSecretMaterial);
    }

    // Reads a string property the endpoint always writes, failing the test here when the property is null.
    private static string GetRequiredString(JsonElement jsonElement, string propertyName)
    {
        var value = jsonElement.GetProperty(propertyName).GetString();
        Assert.NotNull(value);
        return value;
    }

    // Revoking an unknown token is a no-op once the client authenticates (RFC 7009 §2.2), so the response reports only
    // whether the client secret was accepted: no error, or the error the endpoint returned.
    private static async Task<string?> AuthenticateAsync(
        HttpClient client,
        string clientId,
        string clientSecret
    )
    {
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["token"] = "not-a-real-token",
            }
        );
        using var response = await client.PostAsync("/oauth2/revoke", content);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            return null;
        }

        var jsonElement = JsonElement.Parse(await response.Content.ReadAsStringAsync());
        return jsonElement.GetProperty("error").GetString();
    }

    #endregion
}
