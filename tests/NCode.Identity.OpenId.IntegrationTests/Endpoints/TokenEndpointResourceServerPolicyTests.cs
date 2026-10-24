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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Verifies that a resource server's settings bound the access tokens issued for it, combined with the client's at
/// issuance (ADR-0158): a lifetime ceiling, a signing-algorithm ceiling, and a sender-constraint requirement with the
/// confirmation methods the resource server verifies.
/// </summary>
public class TokenEndpointResourceServerPolicyTests
{
    private const string TokenPath = "/oauth2/token";
    private const string ClientSecret = "confidential-secret-value";

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static FormUrlEncodedContent ClientSecretPostBody(string clientId) =>
        new(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                ["scope"] = TestServerSettingsProvider.ApiScope,
                ["client_id"] = clientId,
                ["client_secret"] = ClientSecret,
            }
        );

    private static async Task<HostApplicationFactory> SeedClientSecretPostClientAsync(
        string clientId,
        IReadOnlyDictionary<string, object> resourceServerSettings
    )
    {
        var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientAsync(clientId, ClientSecret);
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            $"{clientId}-rs-api",
            $"https://api.{clientId}.integration.test",
            resourceServerSettings,
            TestServerSettingsProvider.ApiScope
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
    private static async Task<HostApplicationFactory> SeedCertificateBoundClientAsync(
        string clientId,
        X509Certificate2 certificate,
        IReadOnlyDictionary<string, object> resourceServerSettings
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
            OpenIdConstants.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            $"{clientId}-rs-api",
            $"https://api.{clientId}.integration.test",
            resourceServerSettings,
            TestServerSettingsProvider.ApiScope
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
                    ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                    ["scope"] = TestServerSettingsProvider.ApiScope,
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

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static JsonDocument DecodeJwtPayload(string jwt) =>
        JsonDocument.Parse(Base64Url.DecodeFromChars(jwt.Split('.')[1]));

    [Fact]
    public async Task PostToken_WhenTheResourceServerCapsTheLifetime_TheTokenExpiresSooner()
    {
        const string clientId = "it-rs-policy-lifetime";
        using var factory = await SeedClientSecretPostClientAsync(
            clientId,
            new Dictionary<string, object> { [OpenIdSettingNames.AccessTokenLifetime] = "00:01:00" }
        );

        using var response = await CreateClient(factory)
            .PostAsync(TokenPath, ClientSecretPostBody(clientId));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(60, document.RootElement.GetProperty("expires_in").GetInt32());
    }

    [Fact]
    public async Task PostToken_WhenTheResourceServerPermitsNoAlgorithmTheTenantSignsWith_FailsWithServerError()
    {
        // The test tenant signs with an RS256 key only; the resource server's ceiling is never widened to fit it.
        const string clientId = "it-rs-policy-signing";
        using var factory = await SeedClientSecretPostClientAsync(
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.AccessTokenSigningAlgValuesSupported] = new[] { "PS256" },
            }
        );

        using var response = await CreateClient(factory)
            .PostAsync(TokenPath, ClientSecretPostBody(clientId));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        Assert.Equal(
            OpenIdConstants.ErrorCodes.ServerError,
            document.RootElement.GetProperty("error").GetString()
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
            }
        );

        using var response = await CreateClient(factory)
            .PostAsync(TokenPath, ClientSecretPostBody(clientId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        Assert.Equal(
            OpenIdConstants.ErrorCodes.InvalidRequest,
            document.RootElement.GetProperty("error").GetString()
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
                    OpenIdConstants.ConfirmationMethods.X509CertificateSha256Thumbprint,
                },
            }
        );

        using var response = await CreateClient(factory)
            .SendAsync(BuildCertificateTokenRequest(clientId, certificate));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        using var payload = DecodeJwtPayload(
            document.RootElement.GetProperty("access_token").GetString() ?? string.Empty
        );
        Assert.True(
            payload
                .RootElement.GetProperty("cnf")
                .TryGetProperty(
                    OpenIdConstants.ConfirmationMethods.X509CertificateSha256Thumbprint,
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
            OpenIdConstants.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            $"{clientId}-rs-api",
            $"https://api.{clientId}.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        using var response = await CreateClient(factory)
            .SendAsync(BuildCertificateTokenRequest(clientId, certificate));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        Assert.Equal(
            OpenIdConstants.ErrorCodes.InvalidRequest,
            document.RootElement.GetProperty("error").GetString()
        );
        Assert.StartsWith(
            "The client's access tokens must be sender-constrained",
            document.RootElement.GetProperty("error_description").GetString(),
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
                    OpenIdConstants.ConfirmationMethods.JwkThumbprint,
                },
            }
        );

        using var response = await CreateClient(factory)
            .SendAsync(BuildCertificateTokenRequest(clientId, certificate));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        Assert.Equal(
            OpenIdConstants.ErrorCodes.InvalidRequest,
            document.RootElement.GetProperty("error").GetString()
        );
    }
}
