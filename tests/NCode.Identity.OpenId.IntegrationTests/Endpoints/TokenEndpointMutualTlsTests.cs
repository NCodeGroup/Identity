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

using System.Formats.Asn1;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Encoders;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Verifies mutual-TLS client authentication (RFC 8705 §2): <c>self_signed_tls_client_auth</c> matches the presented
/// certificate against the client's registered certificate by thumbprint, and <c>tls_client_auth</c> validates its
/// chain to the client's configured trust anchors and matches its subject distinguished name against the client's
/// registered metadata. The TestServer terminates no TLS, so the
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

    private static X509Certificate2 CreateAuthority(string commonName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={commonName}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                true
            )
        );
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false)
        );
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1)
        );
    }

    private static X509Certificate2 CreateIssuedCertificate(
        X509Certificate2 issuer,
        string commonName,
        bool isAuthority = false,
        string? crlDistributionPoint = null
    ) =>
        CreateIssuedCertificate(
            issuer,
            new X500DistinguishedName($"CN={commonName}"),
            isAuthority,
            crlDistributionPoint
        );

    private static X509Certificate2 CreateIssuedCertificate(
        X509Certificate2 issuer,
        X500DistinguishedName subjectName,
        bool isAuthority = false,
        string? crlDistributionPoint = null
    )
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            subjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(isAuthority, false, 0, true)
        );
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false)
        );
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false)
        );
        if (crlDistributionPoint is not null)
        {
            request.CertificateExtensions.Add(
                CertificateRevocationListBuilder.BuildCrlDistributionPointExtension([
                    crlDistributionPoint,
                ])
            );
        }
        if (!isAuthority)
        {
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new("1.3.6.1.5.5.7.3.2") },
                    false
                )
            );
        }

        var serialNumber = new byte[8];
        RandomNumberGenerator.Fill(serialNumber);
        serialNumber[0] &= 0x7F;
        using var certificate = request.Create(
            issuer,
            DateTimeOffset.UtcNow.AddHours(-1),
            isAuthority ? DateTimeOffset.UtcNow.AddMonths(6) : DateTimeOffset.UtcNow.AddDays(30),
            serialNumber
        );
        return certificate.CopyWithPrivateKey(rsa);
    }

    private static X500DistinguishedName CreateUtf8SubjectName()
    {
        // The builder adds in the order of the RFC 4514 string form, so this is "CN=it-mtls-pki-utf8, O=Example Org".
        var builder = new X500DistinguishedNameBuilder();
        builder.Add("2.5.4.3", "it-mtls-pki-utf8", UniversalTagNumber.UTF8String);
        builder.Add("2.5.4.10", "Example Org", UniversalTagNumber.UTF8String);
        return builder.Build();
    }

    private static async Task<HostApplicationFactory> CreatePkiFactoryAsync(
        string clientId,
        string subjectDn,
        IReadOnlyCollection<string> trustAnchors
    )
    {
        var factory = new HostApplicationFactory();
        await factory.SeedTlsClientAuthSubjectDnClientAsync(
            clientId,
            subjectDn,
            trustAnchors,
            OpenIdProtocol.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            $"{clientId}-rs-api",
            $"https://api.{clientId}.integration.test",
            TestServerSettingsProvider.ApiScope
        );
        return factory;
    }

    private static async Task<HostApplicationFactory> CreateRevocationCheckedFactoryAsync(
        string clientId,
        X509Certificate2 authority,
        X509Certificate2 certificate,
        byte[] crl
    )
    {
        var factory = new HostApplicationFactory { CertificateRevocationListResponder = _ => crl };
        await factory.SeedTlsClientAuthSubjectDnClientAsync(
            clientId,
            certificate.SubjectName.Name,
            [authority.ExportCertificatePem()],
            new Dictionary<string, object>
            {
                ["tls_client_auth_revocation_mode"] = OpenIdConstants
                    .ClientCertificateRevocationMode
                    .Online,
            },
            OpenIdProtocol.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            $"{clientId}-rs-api",
            $"https://api.{clientId}.integration.test",
            TestServerSettingsProvider.ApiScope
        );
        return factory;
    }

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static HttpRequestMessage BuildTokenRequest(
        string clientId,
        X509Certificate2? forwardedCertificate
    )
    {
        var headerValue = forwardedCertificate is null
            ? null
            : Convert.ToBase64String(forwardedCertificate.RawData);
        return BuildTokenRequest(clientId, headerValue);
    }

    private static HttpRequestMessage BuildTokenRequest(
        string clientId,
        string? forwardedHeaderValue
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                    ["scope"] = TestServerSettingsProvider.ApiScope,
                    ["client_id"] = clientId,
                }
            ),
        };

        if (forwardedHeaderValue is not null)
        {
            request.Headers.TryAddWithoutValidation(
                HostApplicationFactory.MutualTlsCertificateHeaderName,
                forwardedHeaderValue
            );
        }

        return request;
    }

    [Fact]
    public async Task PostToken_SelfSignedTlsClientAuth_WithRegisteredCertificate_ReturnsAccessToken()
    {
        const string clientId = "it-mtls-self-signed";
        using var certificate = CreateCertificate(clientId);

        using var factory = new HostApplicationFactory();
        await factory.SeedSelfSignedTlsClientAsync(
            clientId,
            certificate,
            OpenIdProtocol.GrantTypes.ClientCredentials
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

        var jsonElement = JsonElement.Parse(body);
        Assert.False(string.IsNullOrEmpty(jsonElement.GetProperty("access_token").GetString()));
    }

    [Fact]
    public async Task PostToken_SelfSignedTlsClientAuth_WithForwardedChain_AuthenticatesWithLeaf()
    {
        const string clientId = "it-mtls-self-signed-chain";
        using var certificate = CreateCertificate(clientId);
        using var otherCertificate = CreateCertificate("it-mtls-chain-other");

        using var factory = new HostApplicationFactory();
        await factory.SeedSelfSignedTlsClientAsync(
            clientId,
            certificate,
            OpenIdProtocol.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            "it-mtls-ssc-rs-api",
            "https://api.mtls-ssc.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        // Traefik's passTLSClientCert (pem: true) forwards each certificate of the presented chain as base64 DER,
        // comma-separated, the client's own first.
        var leaf = Convert.ToBase64String(certificate.RawData);
        var other = Convert.ToBase64String(otherCertificate.RawData);
        var client = CreateClient(factory);
        using var response = await client.SendAsync(BuildTokenRequest(clientId, $"{leaf},{other}"));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task PostToken_SelfSignedTlsClientAuth_WithMalformedForwardedCertificate_ReturnsInvalidRequest()
    {
        const string clientId = "it-mtls-self-signed-malformed";
        using var certificate = CreateCertificate(clientId);

        using var factory = new HostApplicationFactory();
        await factory.SeedSelfSignedTlsClientAsync(
            clientId,
            certificate,
            OpenIdProtocol.GrantTypes.ClientCredentials
        );

        var client = CreateClient(factory);
        using var response = await client.SendAsync(
            BuildTokenRequest(clientId, "-----BEGIN CERTIFICATE-----")
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var jsonElement = JsonElement.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRequest,
            jsonElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PostToken_SelfSignedTlsClientAuth_WithWrongCertificate_Returns400()
    {
        const string clientId = "it-mtls-self-signed-wrong";
        using var registeredCertificate = CreateCertificate(clientId);
        using var presentedCertificate = CreateCertificate(clientId);

        using var factory = new HostApplicationFactory();
        await factory.SeedSelfSignedTlsClientAsync(
            clientId,
            registeredCertificate,
            OpenIdProtocol.GrantTypes.ClientCredentials
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

        var jsonElement = JsonElement.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidClient,
            jsonElement.GetProperty("error").GetString()
        );
    }

    private const string JwksUri = "https://client.mtls-jwks.integration.test/jwks.json";

    private static string BuildJwks(X509Certificate2 certificate, bool includeCertificate)
    {
        using var rsa = certificate.GetRSAPublicKey();
        var parameters = rsa!.ExportParameters(includePrivateParameters: false);
        var jwk = new Dictionary<string, object>
        {
            ["kty"] = "RSA",
            ["use"] = "sig",
            ["kid"] = "it-mtls-jwks-key",
            ["n"] = Base64Url.Encode(parameters.Modulus!),
            ["e"] = Base64Url.Encode(parameters.Exponent!),
        };
        if (includeCertificate)
            jwk["x5c"] = new[] { Convert.ToBase64String(certificate.RawData) };
        return JsonSerializer.Serialize(
            new Dictionary<string, object> { ["keys"] = new[] { jwk } }
        );
    }

    [Theory]
    [InlineData(true, HttpStatusCode.OK)]
    [InlineData(false, HttpStatusCode.BadRequest)]
    public async Task PostToken_SelfSignedTlsClientAuth_WithJwksUri_MatchesPublishedCertificate(
        bool publishCertificate,
        HttpStatusCode expectedStatusCode
    )
    {
        const string clientId = "it-mtls-self-signed-jwks";
        using var certificate = CreateCertificate(clientId);
        var jwks = BuildJwks(certificate, publishCertificate);

        using var factory = new HostApplicationFactory { ClientJwksResponder = _ => jwks };
        await factory.SeedSelfSignedTlsClientWithJwksUriAsync(
            clientId,
            JwksUri,
            OpenIdProtocol.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            "it-mtls-jwks-rs-api",
            "https://api.mtls-jwks.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        var client = CreateClient(factory);
        using var response = await client.SendAsync(BuildTokenRequest(clientId, certificate));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expectedStatusCode, body);
    }

    [Fact]
    public async Task PostToken_TlsClientAuth_WithMatchingSubjectDn_ReturnsAccessToken()
    {
        const string clientId = "it-mtls-pki";
        using var authority = CreateAuthority("it-mtls-pki-root");
        using var certificate = CreateIssuedCertificate(authority, clientId);

        using var factory = await CreatePkiFactoryAsync(
            clientId,
            certificate.SubjectName.Name,
            [authority.ExportCertificatePem()]
        );

        var client = CreateClient(factory);
        using var response = await client.SendAsync(BuildTokenRequest(clientId, certificate));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var jsonElement = JsonElement.Parse(body);
        Assert.False(string.IsNullOrEmpty(jsonElement.GetProperty("access_token").GetString()));
    }

    [Fact]
    public async Task PostToken_TlsClientAuth_WithAUtf8SubjectRegisteredInAnotherForm_ReturnsAccessToken()
    {
        const string clientId = "it-mtls-pki-utf8";
        using var authority = CreateAuthority("it-mtls-pki-utf8-root");
        using var certificate = CreateIssuedCertificate(authority, CreateUtf8SubjectName());

        using var factory = await CreatePkiFactoryAsync(
            clientId,
            "cn=IT-MTLS-PKI-UTF8 ,  o=example   org",
            [authority.ExportCertificatePem()]
        );

        var client = CreateClient(factory);
        using var response = await client.SendAsync(BuildTokenRequest(clientId, certificate));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task PostToken_TlsClientAuth_WithAnotherOrganization_ReturnsInvalidClient()
    {
        const string clientId = "it-mtls-pki-otherorg";
        using var authority = CreateAuthority("it-mtls-pki-otherorg-root");
        using var certificate = CreateIssuedCertificate(authority, CreateUtf8SubjectName());

        using var factory = await CreatePkiFactoryAsync(
            clientId,
            "CN=it-mtls-pki-utf8, O=Other Org",
            [authority.ExportCertificatePem()]
        );

        var client = CreateClient(factory);
        using var response = await client.SendAsync(BuildTokenRequest(clientId, certificate));

        await AssertInvalidClientAsync(response);
    }

    [Fact]
    public async Task PostToken_TlsClientAuth_WithForwardedIntermediate_ReturnsAccessToken()
    {
        const string clientId = "it-mtls-pki-chain";
        using var authority = CreateAuthority("it-mtls-pki-chain-root");
        using var intermediate = CreateIssuedCertificate(
            authority,
            "it-mtls-pki-chain-issuing",
            isAuthority: true
        );
        using var certificate = CreateIssuedCertificate(intermediate, clientId);

        using var factory = await CreatePkiFactoryAsync(
            clientId,
            certificate.SubjectName.Name,
            [authority.ExportCertificatePem()]
        );

        // The forwarded chain is the client's own certificate first, then the intermediate that issued it.
        var leaf = Convert.ToBase64String(certificate.RawData);
        var issuing = Convert.ToBase64String(intermediate.RawData);
        var client = CreateClient(factory);
        using var response = await client.SendAsync(
            BuildTokenRequest(clientId, $"{leaf},{issuing}")
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task PostToken_TlsClientAuth_WithSelfSignedMatchingSubjectDn_ReturnsInvalidClient()
    {
        const string clientId = "it-mtls-pki-forged";
        using var authority = CreateAuthority("it-mtls-pki-forged-root");
        using var forged = CreateCertificate(clientId);

        using var factory = await CreatePkiFactoryAsync(
            clientId,
            forged.SubjectName.Name,
            [authority.ExportCertificatePem()]
        );

        var client = CreateClient(factory);
        using var response = await client.SendAsync(BuildTokenRequest(clientId, forged));

        await AssertInvalidClientAsync(response);
    }

    [Fact]
    public async Task PostToken_TlsClientAuth_WithNoTrustAnchors_ReturnsInvalidClient()
    {
        const string clientId = "it-mtls-pki-noanchor";
        using var authority = CreateAuthority("it-mtls-pki-noanchor-root");
        using var certificate = CreateIssuedCertificate(authority, clientId);

        using var factory = await CreatePkiFactoryAsync(clientId, certificate.SubjectName.Name, []);

        var client = CreateClient(factory);
        using var response = await client.SendAsync(BuildTokenRequest(clientId, certificate));

        await AssertInvalidClientAsync(response);
    }

    [Fact]
    public async Task PostToken_TlsClientAuth_WithNoCertificate_Returns400()
    {
        const string clientId = "it-mtls-pki-nocert";
        using var authority = CreateAuthority("it-mtls-pki-nocert-root");

        using var factory = await CreatePkiFactoryAsync(
            clientId,
            $"CN={clientId}",
            [authority.ExportCertificatePem()]
        );

        var client = CreateClient(factory);
        using var response = await client.SendAsync(
            BuildTokenRequest(clientId, forwardedCertificate: null)
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #region Revocation

    [Fact]
    public async Task PostToken_TlsClientAuth_WhenTheCrlRevokesTheCertificate_ReturnsInvalidClient()
    {
        const string clientId = "it-mtls-pki-revoked";
        using var authority = CreateAuthority("it-mtls-pki-revoked-root");
        using var certificate = CreateIssuedCertificate(
            authority,
            clientId,
            crlDistributionPoint: "http://crl.it-mtls.test/root.crl"
        );

        var builder = new CertificateRevocationListBuilder();
        builder.AddEntry(certificate);
        var crl = builder.Build(
            authority,
            BigInteger.One,
            DateTimeOffset.UtcNow.AddDays(1),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );

        using var factory = await CreateRevocationCheckedFactoryAsync(
            clientId,
            authority,
            certificate,
            crl
        );

        var client = CreateClient(factory);
        using var response = await client.SendAsync(BuildTokenRequest(clientId, certificate));

        await AssertInvalidClientAsync(response);
    }

    [Fact]
    public async Task PostToken_TlsClientAuth_WhenTheCrlDoesNotRevokeTheCertificate_ReturnsAccessToken()
    {
        const string clientId = "it-mtls-pki-notrevoked";
        using var authority = CreateAuthority("it-mtls-pki-notrevoked-root");
        using var certificate = CreateIssuedCertificate(
            authority,
            clientId,
            crlDistributionPoint: "http://crl.it-mtls.test/root.crl"
        );

        var builder = new CertificateRevocationListBuilder();
        var crl = builder.Build(
            authority,
            BigInteger.One,
            DateTimeOffset.UtcNow.AddDays(1),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );

        using var factory = await CreateRevocationCheckedFactoryAsync(
            clientId,
            authority,
            certificate,
            crl
        );

        var client = CreateClient(factory);
        using var response = await client.SendAsync(BuildTokenRequest(clientId, certificate));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    #endregion

    private static async Task AssertInvalidClientAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);

        var jsonElement = JsonElement.Parse(body);
        Assert.Equal("invalid_client", jsonElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Discovery_AdvertisesMutualTlsAuthMethods()
    {
        using var factory = new HostApplicationFactory();
        var client = CreateClient(factory);

        using var response = await client.GetAsync("/.well-known/openid-configuration");
        var jsonElement = JsonElement.Parse(await response.Content.ReadAsStringAsync());

        var methods = jsonElement
            .GetProperty("token_endpoint_auth_methods_supported")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToList();

        Assert.Contains(OpenIdProtocol.ClientAuthenticationMethods.TlsClientAuth, methods);
        Assert.Contains(
            OpenIdProtocol.ClientAuthenticationMethods.SelfSignedTlsClientAuth,
            methods
        );
    }
}
