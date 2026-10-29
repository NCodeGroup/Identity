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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose;
using NCode.Identity.Jwt;
using NCode.Identity.OpenId.Client;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Client;

/// <summary>
/// Proves mutual-TLS client authentication of the OpenID client library against the NCode OpenID provider hosted
/// in-process.
/// </summary>
public class MutualTlsClientEndToEndTests
{
    private const string KeyRing = "e2e-mtls";

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

    [Fact]
    public async Task RequestTokenAsync_SelfSignedTlsClientAuth_ReturnsAccessToken()
    {
        const string clientId = "e2e-mtls-client";
        using var certificate = CreateCertificate(clientId);

        using var factory = new HostApplicationFactory();
        await factory.SeedSelfSignedTlsClientAsync(
            clientId,
            certificate,
            OpenIdProtocol.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            clientId + "-rs",
            "https://api.client.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary().AddOpenIdClientLibrary();
        services
            .AddHttpClient(OpenIdClientDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => factory.Server.CreateHandler());
        services
            .AddHttpClient(
                OpenIdClientDefaults.MutualTlsHttpClientNamePrefix
                    + Convert.ToHexString(SHA256.HashData(certificate.RawData))
            )
            .ConfigurePrimaryHttpMessageHandler(() =>
                new ForwardedClientCertificateHandler(certificate.RawData)
                {
                    InnerHandler = factory.Server.CreateHandler(),
                }
            );
        services.AddKeyedSingleton<ISecretKeyCollectionProvider>(
            KeyRing,
            (serviceProvider, _) =>
                serviceProvider
                    .GetRequiredService<ISecretKeyCollectionProviderFactory>()
                    .CreateStatic([
                        serviceProvider
                            .GetRequiredService<ISecretKeyFactory>()
                            .Create(new KeyMetadata { KeyId = "mtls" }, certificate),
                    ])
        );

        await using var provider = services.BuildServiceProvider();
        var registration = new ClientRegistration(
            HostApplicationFactory.TestServerIssuer,
            clientId,
            OpenIdProtocol.ClientAuthenticationMethods.SelfSignedTlsClientAuth
        )
        {
            KeyRing = KeyRing,
        };

        var result = await provider
            .GetRequiredService<ITokenClient>()
            .RequestTokenAsync(
                registration,
                new ClientCredentialsTokenRequest { Scope = TestServerSettingsProvider.ApiScope },
                CancellationToken.None
            );

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.False(string.IsNullOrEmpty(result.Response.AccessToken));
    }

    /// <summary>
    /// Forwards the client certificate in the header the test host trusts, because <c>TestServer</c> terminates no
    /// TLS.
    /// </summary>
    private sealed class ForwardedClientCertificateHandler(byte[] rawData) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            request.Headers.TryAddWithoutValidation(
                HostApplicationFactory.MutualTlsCertificateHeaderName,
                Convert.ToBase64String(rawData)
            );
            return base.SendAsync(request, cancellationToken);
        }
    }
}
