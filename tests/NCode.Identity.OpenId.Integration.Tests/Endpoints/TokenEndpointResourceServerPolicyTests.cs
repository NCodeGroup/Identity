// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Verifies that a resource server's settings bound the access tokens issued for it, combined with the client's at
/// issuance: a lifetime ceiling, a signing-algorithm ceiling, and a sender-constraint requirement with the
/// confirmation methods the resource server verifies.
/// </summary>
// See ADR-1158.
public class TokenEndpointResourceServerPolicyTests
{
    private const string TokenPath = "/oauth2/token";
    private const string ClientSecret = "confidential-secret-value";

    private static IReadOnlyDictionary<string, string?> NoConfiguration { get; } =
        new Dictionary<string, string?>();

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static FormUrlEncodedContent ClientSecretPostBody(string clientId) =>
        new(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                ["scope"] = TestPlatformSettingsProvider.ApiScope,
                ["client_id"] = clientId,
                ["client_secret"] = ClientSecret,
            }
        );

    private static async ValueTask<HostApplicationFactory> SeedClientSecretPostClientAsync(
        string clientId,
        IReadOnlyDictionary<string, object> resourceServerSettings,
        IReadOnlyDictionary<string, string?> configuration,
        CancellationToken cancellationToken
    )
    {
        HostApplicationFactory factory = new ConfiguredHostApplicationFactory(configuration);
        await factory.SeedConfidentialClientAsync(clientId, ClientSecret, [], cancellationToken);
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            $"{clientId}-rs-api",
            $"https://api.{clientId}.integration.test",
            resourceServerSettings,
            [TestPlatformSettingsProvider.ApiScope],
            cancellationToken
        );
        return factory;
    }

    private static X509Certificate2 CreateCertificate(string commonName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={commonName}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1)
        );
    }

    // A client that authenticates with its certificate and whose access tokens are bound to it (RFC 8705 §3).
    private static async ValueTask<HostApplicationFactory> SeedCertificateBoundClientAsync(
        string clientId,
        X509Certificate2 certificate,
        IReadOnlyDictionary<string, object> resourceServerSettings,
        CancellationToken cancellationToken
    )
    {
        var factory = new HostApplicationFactory();
        await factory.SeedSelfSignedTlsClientAsync(
            clientId,
            certificate,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.RequireTlsClientCertificateBoundAccessTokens] = true,
            },
            [OpenIdProtocol.GrantTypes.ClientCredentials],
            cancellationToken
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            $"{clientId}-rs-api",
            $"https://api.{clientId}.integration.test",
            resourceServerSettings,
            [TestPlatformSettingsProvider.ApiScope],
            cancellationToken
        );
        return factory;
    }

    private static HttpRequestMessage BuildCertificateTokenRequest(
        string clientId,
        X509Certificate2 certificate
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                    ["scope"] = TestPlatformSettingsProvider.ApiScope,
                    ["client_id"] = clientId,
                }
            ),
        };
        request.Headers.TryAddWithoutValidation(
            HostApplicationFactory.MutualTlsCertificateHeaderName,
            Convert.ToBase64String(certificate.Export(X509ContentType.Cert))
        );
        return request;
    }

    private static async ValueTask<JsonElement> ReadJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonElement.Parse(body);
    }

    private static JsonElement DecodeJwtPayload(string jwt) =>
        JsonElement.Parse(Base64Url.DecodeFromChars(jwt.Split('.')[1]));

    [Fact]
    public async Task PostToken_WhenTheResourceServerCapsTheLifetime_TheTokenExpiresSooner()
    {
        const string clientId = "it-rs-policy-lifetime";
        using var factory = await SeedClientSecretPostClientAsync(
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.AccessTokenLifetime] = "00:01:00",
            },
            configuration: NoConfiguration,
            CancellationToken.None
        );

        using var response = await CreateClient(factory)
            .PostAsync(TokenPath, ClientSecretPostBody(clientId));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var jsonElement = JsonElement.Parse(body);
        Assert.Equal(60, jsonElement.GetProperty("expires_in").GetInt32());
    }

    [Fact]
    public async Task PostToken_WhenTheResourceServerPermitsNoAlgorithmTheTenantSignsWith_FailsWithServerError()
    {
        // The test provisions an RS256 key only (ADR-1169); the resource server's ceiling is never widened to fit it.
        const string clientId = "it-rs-policy-signing";
        using var factory = await SeedClientSecretPostClientAsync(
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.AccessTokenSigningAlgValuesSupported] = new[] { "PS256" },
            },
            new Dictionary<string, string?>
            {
                ["OpenId:Tenant:KeyProvisioning:Keys:0:Algorithm"] = "RS256",
            },
            CancellationToken.None
        );

        using var response = await CreateClient(factory)
            .PostAsync(TokenPath, ClientSecretPostBody(clientId));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var jsonElement = await ReadJsonAsync(response, CancellationToken.None);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.ServerError,
            jsonElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PostToken_WhenTheResourceServerRequiresASenderConstraint_RefusesAnUnboundRequest()
    {
        const string clientId = "it-rs-policy-unbound";
        using var factory = await SeedClientSecretPostClientAsync(
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.AccessTokenSenderConstraintRequired] = true,
            },
            configuration: NoConfiguration,
            CancellationToken.None
        );

        using var response = await CreateClient(factory)
            .PostAsync(TokenPath, ClientSecretPostBody(clientId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonElement = await ReadJsonAsync(response, CancellationToken.None);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRequest,
            jsonElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PostToken_WhenTheResourceServerVerifiesCertificateBinding_IssuesACertificateBoundToken()
    {
        const string clientId = "it-rs-policy-mtls";
        using var certificate = CreateCertificate(clientId);
        using var factory = await SeedCertificateBoundClientAsync(
            clientId,
            certificate,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.AccessTokenSenderConstraintRequired] = true,
                [OpenIdSettingNames.AccessTokenConfirmationMethodsSupported] = new[]
                {
                    OpenIdProtocol.ConfirmationMethods.X509CertificateSha256Thumbprint,
                },
            },
            CancellationToken.None
        );

        var client = CreateClient(factory);
        using var request = BuildCertificateTokenRequest(clientId, certificate);
        using var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var jsonElement = JsonElement.Parse(body);
        var payload = DecodeJwtPayload(
            jsonElement.GetProperty("access_token").GetString() ?? string.Empty
        );
        Assert.True(
            payload
                .GetProperty("cnf")
                .TryGetProperty(
                    OpenIdProtocol.ConfirmationMethods.X509CertificateSha256Thumbprint,
                    out _
                )
        );
    }

    [Fact]
    public async Task PostToken_WhenTheClientSelectsFapi2_RefusesAnUnboundRequestAtTheTokenEndpoint()
    {
        // FAPI 2.0's sender-constraint rule is a ceiling on the client's settings, enforced before the grant is spent.
        const string clientId = "it-rs-policy-fapi2";
        using var certificate = CreateCertificate(clientId);
        using var factory = new HostApplicationFactory();
        await factory.SeedSelfSignedTlsClientAsync(
            clientId,
            certificate,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.SecurityProfiles] = new[]
                {
                    OpenIdConstants.SecurityProfiles.Fapi2,
                },
            },
            [OpenIdProtocol.GrantTypes.ClientCredentials],
            CancellationToken.None
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            $"{clientId}-rs-api",
            $"https://api.{clientId}.integration.test",
            [TestPlatformSettingsProvider.ApiScope],
            CancellationToken.None
        );

        var client = CreateClient(factory);
        using var request = BuildCertificateTokenRequest(clientId, certificate);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonElement = await ReadJsonAsync(response, CancellationToken.None);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRequest,
            jsonElement.GetProperty("error").GetString()
        );
        Assert.StartsWith(
            "The client's access tokens must be sender-constrained",
            jsonElement.GetProperty("error_description").GetString(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task PostToken_WhenTheResourceServerVerifiesOnlyDPoP_RefusesACertificateBoundToken()
    {
        const string clientId = "it-rs-policy-dpop-only";
        using var certificate = CreateCertificate(clientId);
        using var factory = await SeedCertificateBoundClientAsync(
            clientId,
            certificate,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.AccessTokenSenderConstraintRequired] = true,
                [OpenIdSettingNames.AccessTokenConfirmationMethodsSupported] = new[]
                {
                    OpenIdProtocol.ConfirmationMethods.JwkThumbprint,
                },
            },
            CancellationToken.None
        );

        var client = CreateClient(factory);
        using var request = BuildCertificateTokenRequest(clientId, certificate);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonElement = await ReadJsonAsync(response, CancellationToken.None);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRequest,
            jsonElement.GetProperty("error").GetString()
        );
    }
}
