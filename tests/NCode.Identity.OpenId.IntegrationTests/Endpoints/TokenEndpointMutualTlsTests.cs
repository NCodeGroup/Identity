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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Verifies mutual-TLS client authentication (RFC 8705 §2): <c>self_signed_tls_client_auth</c> matches the presented
/// certificate against the client's registered certificate by thumbprint, and <c>tls_client_auth</c> matches its
/// subject distinguished name against the client's registered metadata. The TestServer terminates no TLS, so the
/// certificate is forwarded in a header (the reverse-proxy path the default resolver supports).
/// </summary>
public class TokenEndpointMutualTlsTests
{
    private const string TokenPath = "/oauth2/token";

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

    private static HttpClient CreateClient(PlaygroundApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static HttpRequestMessage BuildTokenRequest(
        string clientId,
        X509Certificate2? forwardedCertificate
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

        if (forwardedCertificate is not null)
        {
            var base64Der = Convert.ToBase64String(
                forwardedCertificate.Export(X509ContentType.Cert)
            );
            request.Headers.TryAddWithoutValidation(
                PlaygroundApplicationFactory.MutualTlsCertificateHeaderName,
                base64Der
            );
        }

        return request;
    }

    [Fact]
    public async Task PostToken_SelfSignedTlsClientAuth_WithRegisteredCertificate_ReturnsAccessToken()
    {
        const string clientId = "it-mtls-self-signed";
        using var certificate = CreateCertificate(clientId);

        using var factory = new PlaygroundApplicationFactory();
        await factory.SeedSelfSignedTlsClientAsync(
            clientId,
            certificate,
            OpenIdConstants.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            "it-mtls-ss-rs-api",
            "https://api.mtls-ss.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        var client = CreateClient(factory);
        using var response = await client.SendAsync(BuildTokenRequest(clientId, certificate));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        Assert.False(
            string.IsNullOrEmpty(document.RootElement.GetProperty("access_token").GetString())
        );
    }

    [Fact]
    public async Task PostToken_SelfSignedTlsClientAuth_WithWrongCertificate_Returns400()
    {
        const string clientId = "it-mtls-self-signed-wrong";
        using var registeredCertificate = CreateCertificate(clientId);
        using var presentedCertificate = CreateCertificate(clientId);

        using var factory = new PlaygroundApplicationFactory();
        await factory.SeedSelfSignedTlsClientAsync(
            clientId,
            registeredCertificate,
            OpenIdConstants.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            "it-mtls-ssw-rs-api",
            "https://api.mtls-ssw.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        var client = CreateClient(factory);
        using var response = await client.SendAsync(
            BuildTokenRequest(clientId, presentedCertificate)
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdConstants.ErrorCodes.InvalidClient,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PostToken_TlsClientAuth_WithMatchingSubjectDn_ReturnsAccessToken()
    {
        const string clientId = "it-mtls-pki";
        using var certificate = CreateCertificate(clientId);
        var subjectDn = certificate.SubjectName.Name;

        using var factory = new PlaygroundApplicationFactory();
        await factory.SeedTlsClientAuthSubjectDnClientAsync(
            clientId,
            subjectDn,
            OpenIdConstants.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            "it-mtls-pki-rs-api",
            "https://api.mtls-pki.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        var client = CreateClient(factory);
        using var response = await client.SendAsync(BuildTokenRequest(clientId, certificate));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        Assert.False(
            string.IsNullOrEmpty(document.RootElement.GetProperty("access_token").GetString())
        );
    }

    [Fact]
    public async Task PostToken_TlsClientAuth_WithNoCertificate_Returns400()
    {
        const string clientId = "it-mtls-pki-nocert";
        using var certificate = CreateCertificate(clientId);

        using var factory = new PlaygroundApplicationFactory();
        await factory.SeedTlsClientAuthSubjectDnClientAsync(
            clientId,
            certificate.SubjectName.Name,
            OpenIdConstants.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            "it-mtls-pki-nc-rs-api",
            "https://api.mtls-pki-nc.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        var client = CreateClient(factory);
        using var response = await client.SendAsync(
            BuildTokenRequest(clientId, forwardedCertificate: null)
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Discovery_AdvertisesMutualTlsAuthMethods()
    {
        using var factory = new PlaygroundApplicationFactory();
        var client = CreateClient(factory);

        using var response = await client.GetAsync("/.well-known/openid-configuration");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var methods = document
            .RootElement.GetProperty("token_endpoint_auth_methods_supported")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToList();

        Assert.Contains(OpenIdConstants.ClientAuthenticationMethods.TlsClientAuth, methods);
        Assert.Contains(
            OpenIdConstants.ClientAuthenticationMethods.SelfSignedTlsClientAuth,
            methods
        );
    }
}
