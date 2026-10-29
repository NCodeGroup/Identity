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
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Client.MutualTls;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.MutualTls;

public class ClientCertificateProviderTests : BaseTests, IAsyncLifetime
{
    private const string Issuer = "https://op.example.test";
    private const string ClientId = "client-1";
    private const string KeyRing = "ring-1";
    private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";
    private const string ClientAuthOid = "1.3.6.1.5.5.7.3.2";

    private ServiceProvider ServiceProvider { get; }
    private MutableTimeProvider TimeProvider { get; } = new(DateTimeOffset.UtcNow);
    private List<IDisposable> Disposables { get; } = [];

    public ClientCertificateProviderTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var disposable in Disposables)
            disposable.Dispose();

        await ServiceProvider.DisposeAsync();
    }

    private ISecretKeyFactory KeyFactory => ServiceProvider.GetRequiredService<ISecretKeyFactory>();

    private T Track<T>(T disposable)
        where T : IDisposable
    {
        Disposables.Add(disposable);
        return disposable;
    }

    private (RSA Rsa, X509Certificate2 Certificate) CreateRsaCertificate(
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null,
        string? ekuOid = null,
        X509KeyUsageFlags? keyUsage = null
    )
    {
        var rsa = Track(RSA.Create(2048));
        var request = new CertificateRequest(
            "CN=test-client",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        AddUsages(request, ekuOid, keyUsage);
        var certificate = Track(CreateSelfSigned(request, notBefore, notAfter));
        return (rsa, certificate);
    }

    private static void AddUsages(
        CertificateRequest request,
        string? ekuOid,
        X509KeyUsageFlags? keyUsage
    )
    {
        if (ekuOid is not null)
        {
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension([new Oid(ekuOid)], critical: false)
            );
        }

        if (keyUsage is not null)
        {
            request.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage.Value, true));
        }
    }

    private X509Certificate2 CreateSelfSigned(
        CertificateRequest request,
        DateTimeOffset? notBefore,
        DateTimeOffset? notAfter
    ) =>
        request.CreateSelfSigned(
            notBefore ?? TimeProvider.UtcNow.AddDays(-1),
            notAfter ?? TimeProvider.UtcNow.AddDays(30)
        );

    private ClientCertificateProvider CreateProvider(params SecretKey[] keys)
    {
        var mockKeyRingProvider = CreateStrictMock<IClientKeyRingProvider>();
        mockKeyRingProvider
            .Setup(x => x.GetKeyRing(KeyRing))
            .Returns(ServiceProvider.GetRequiredService<ISecretKeyCollectionFactory>().Create(keys))
            .Verifiable();

        return Track(new ClientCertificateProvider(mockKeyRingProvider.Object, TimeProvider));
    }

    private static ClientRegistration CreateRegistration(string? keyRing = KeyRing) =>
        new(Issuer, ClientId, OpenIdProtocol.ClientAuthenticationMethods.SelfSignedTlsClientAuth)
        {
            KeyRing = keyRing,
        };

    private static string GetThumbprint(X509Certificate2 certificate) =>
        Convert.ToHexString(SHA256.HashData(certificate.RawData));

    #region SelectHttpClientName Tests

    [Fact]
    public void SelectHttpClientName_WithRsaCertificate_ReturnsPrefixedThumbprintAndCachesCertificate()
    {
        var (rsa, certificate) = CreateRsaCertificate();
        var key = KeyFactory.CreateRsa(new KeyMetadata { KeyId = "k1" }, rsa, certificate);
        var provider = CreateProvider(key);

        var name = provider.SelectHttpClientName(CreateRegistration());

        var thumbprint = GetThumbprint(certificate);
        Assert.Equal(OpenIdClientDefaults.MutualTlsHttpClientNamePrefix + thumbprint, name);
        Assert.True(provider.TryGetCertificate(thumbprint, out var cached));
        Assert.True(cached.HasPrivateKey);
        Assert.Equal(certificate.RawData, cached.RawData);
    }

    [Fact]
    public void SelectHttpClientName_CalledTwice_ReusesTheCachedCertificate()
    {
        var (rsa, certificate) = CreateRsaCertificate();
        var key = KeyFactory.CreateRsa(new KeyMetadata { KeyId = "k1" }, rsa, certificate);
        var mockKeyRingProvider = CreateStrictMock<IClientKeyRingProvider>();
        mockKeyRingProvider
            .Setup(x => x.GetKeyRing(KeyRing))
            .Returns(
                ServiceProvider.GetRequiredService<ISecretKeyCollectionFactory>().Create([key])
            )
            .Verifiable();
        var provider = Track(
            new ClientCertificateProvider(mockKeyRingProvider.Object, TimeProvider)
        );

        provider.SelectHttpClientName(CreateRegistration());
        provider.SelectHttpClientName(CreateRegistration());

        var thumbprint = GetThumbprint(certificate);
        Assert.True(provider.TryGetCertificate(thumbprint, out var first));
        Assert.True(provider.TryGetCertificate(thumbprint, out var second));
        Assert.Same(first, second);
    }

    [Fact]
    public void SelectHttpClientName_WithEcdsaCertificate_ReturnsPrefixedThumbprint()
    {
        var ecdsa = Track(ECDsa.Create(ECCurve.NamedCurves.nistP256));
        var request = new CertificateRequest("CN=test-ec", ecdsa, HashAlgorithmName.SHA256);
        var certificate = Track(CreateSelfSigned(request, null, null));
        var key = KeyFactory.CreateEcc(new KeyMetadata { KeyId = "k1" }, ecdsa, certificate);
        var provider = CreateProvider(key);

        var name = provider.SelectHttpClientName(CreateRegistration());

        var thumbprint = GetThumbprint(certificate);
        Assert.Equal(OpenIdClientDefaults.MutualTlsHttpClientNamePrefix + thumbprint, name);
        Assert.True(provider.TryGetCertificate(thumbprint, out var cached));
        Assert.True(cached.HasPrivateKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SelectHttpClientName_WithoutKeyRing_Throws(string? keyRing)
    {
        var mockKeyRingProvider = CreateStrictMock<IClientKeyRingProvider>();
        var provider = Track(
            new ClientCertificateProvider(mockKeyRingProvider.Object, TimeProvider)
        );

        Assert.Throws<InvalidOperationException>(() =>
            provider.SelectHttpClientName(CreateRegistration(keyRing))
        );
    }

    [Fact]
    public void SelectHttpClientName_WithExpiredCertificate_Throws()
    {
        var (rsa, certificate) = CreateRsaCertificate(
            notBefore: TimeProvider.UtcNow.AddDays(-10),
            notAfter: TimeProvider.UtcNow.AddDays(-1)
        );
        var key = KeyFactory.CreateRsa(new KeyMetadata { KeyId = "k1" }, rsa, certificate);
        var provider = CreateProvider(key);

        Assert.Throws<InvalidOperationException>(() =>
            provider.SelectHttpClientName(CreateRegistration())
        );
    }

    [Fact]
    public void SelectHttpClientName_WithNotYetValidCertificate_SkipsItUntilItIsValid()
    {
        var (rsa, certificate) = CreateRsaCertificate(
            notBefore: TimeProvider.UtcNow.AddDays(1),
            notAfter: TimeProvider.UtcNow.AddDays(30)
        );
        var key = KeyFactory.CreateRsa(new KeyMetadata { KeyId = "k1" }, rsa, certificate);
        var mockKeyRingProvider = CreateStrictMock<IClientKeyRingProvider>();
        mockKeyRingProvider
            .Setup(x => x.GetKeyRing(KeyRing))
            .Returns(
                ServiceProvider.GetRequiredService<ISecretKeyCollectionFactory>().Create([key])
            )
            .Verifiable();
        var provider = Track(
            new ClientCertificateProvider(mockKeyRingProvider.Object, TimeProvider)
        );

        Assert.Throws<InvalidOperationException>(() =>
            provider.SelectHttpClientName(CreateRegistration())
        );

        TimeProvider.UtcNow = TimeProvider.UtcNow.AddDays(2);

        var name = provider.SelectHttpClientName(CreateRegistration());
        Assert.Equal(
            OpenIdClientDefaults.MutualTlsHttpClientNamePrefix + GetThumbprint(certificate),
            name
        );
    }

    [Fact]
    public void SelectHttpClientName_WithExpiredAndValidCertificates_SelectsTheValidOne()
    {
        var (expiredRsa, expiredCertificate) = CreateRsaCertificate(
            notBefore: TimeProvider.UtcNow.AddDays(-10),
            notAfter: TimeProvider.UtcNow.AddDays(-1)
        );
        var (validRsa, validCertificate) = CreateRsaCertificate();
        var expiredKey = KeyFactory.CreateRsa(
            new KeyMetadata { KeyId = "expired", ExpiresWhen = TimeProvider.UtcNow.AddDays(100) },
            expiredRsa,
            expiredCertificate
        );
        var validKey = KeyFactory.CreateRsa(
            new KeyMetadata { KeyId = "valid", ExpiresWhen = TimeProvider.UtcNow.AddDays(10) },
            validRsa,
            validCertificate
        );
        var provider = CreateProvider(expiredKey, validKey);

        var name = provider.SelectHttpClientName(CreateRegistration());

        Assert.Equal(
            OpenIdClientDefaults.MutualTlsHttpClientNamePrefix + GetThumbprint(validCertificate),
            name
        );
    }

    [Fact]
    public void SelectHttpClientName_WithEncryptionKey_Throws()
    {
        var (rsa, certificate) = CreateRsaCertificate();
        var key = KeyFactory.CreateRsa(
            new KeyMetadata { KeyId = "k1", Use = "enc" },
            rsa,
            certificate
        );
        var provider = CreateProvider(key);

        Assert.Throws<InvalidOperationException>(() =>
            provider.SelectHttpClientName(CreateRegistration())
        );
    }

    [Fact]
    public void SelectHttpClientName_WithPublicOnlyKey_Throws()
    {
        var (rsa, certificate) = CreateRsaCertificate();
        using var publicRsa = RSA.Create();
        publicRsa.ImportParameters(rsa.ExportParameters(includePrivateParameters: false));
        var key = KeyFactory.CreateRsaPublicKey(
            new KeyMetadata { KeyId = "k1" },
            publicRsa,
            certificate
        );
        var provider = CreateProvider(key);

        Assert.Throws<InvalidOperationException>(() =>
            provider.SelectHttpClientName(CreateRegistration())
        );
    }

    [Fact]
    public void SelectHttpClientName_WithServerAuthOnlyCertificate_Throws()
    {
        var (rsa, certificate) = CreateRsaCertificate(ekuOid: ServerAuthOid);
        var key = KeyFactory.CreateRsa(new KeyMetadata { KeyId = "k1" }, rsa, certificate);
        var provider = CreateProvider(key);

        Assert.Throws<InvalidOperationException>(() =>
            provider.SelectHttpClientName(CreateRegistration())
        );
    }

    [Fact]
    public void SelectHttpClientName_WithClientAuthCertificate_ReturnsPrefixedThumbprint()
    {
        var (rsa, certificate) = CreateRsaCertificate(
            ekuOid: ClientAuthOid,
            keyUsage: X509KeyUsageFlags.DigitalSignature
        );
        var key = KeyFactory.CreateRsa(new KeyMetadata { KeyId = "k1" }, rsa, certificate);
        var provider = CreateProvider(key);

        var name = provider.SelectHttpClientName(CreateRegistration());

        Assert.Equal(
            OpenIdClientDefaults.MutualTlsHttpClientNamePrefix + GetThumbprint(certificate),
            name
        );
    }

    [Fact]
    public void SelectHttpClientName_WithoutDigitalSignatureKeyUsage_Throws()
    {
        var (rsa, certificate) = CreateRsaCertificate(keyUsage: X509KeyUsageFlags.KeyEncipherment);
        var key = KeyFactory.CreateRsa(new KeyMetadata { KeyId = "k1" }, rsa, certificate);
        var provider = CreateProvider(key);

        Assert.Throws<InvalidOperationException>(() =>
            provider.SelectHttpClientName(CreateRegistration())
        );
    }

    [Fact]
    public void SelectHttpClientName_WithKeyWithoutCertificate_Throws()
    {
        var rsa = Track(RSA.Create(2048));
        var key = KeyFactory.CreateRsa(new KeyMetadata { KeyId = "k1" }, rsa);
        var provider = CreateProvider(key);

        Assert.Throws<InvalidOperationException>(() =>
            provider.SelectHttpClientName(CreateRegistration())
        );
    }

    #endregion

    #region CreatePrimaryHandler Tests

    [Fact]
    public void CreatePrimaryHandler_WithUnknownThumbprint_Throws()
    {
        var mockKeyRingProvider = CreateStrictMock<IClientKeyRingProvider>();
        var provider = Track(
            new ClientCertificateProvider(mockKeyRingProvider.Object, TimeProvider)
        );

        Assert.Throws<InvalidOperationException>(() => provider.CreatePrimaryHandler("UNKNOWN"));
    }

    [Fact]
    public void CreatePrimaryHandler_WithSelectedCertificate_DisablesRedirectsAndCookies()
    {
        var (rsa, certificate) = CreateRsaCertificate();
        var key = KeyFactory.CreateRsa(new KeyMetadata { KeyId = "k1" }, rsa, certificate);
        var provider = CreateProvider(key);
        provider.SelectHttpClientName(CreateRegistration());

        using var handler = provider.CreatePrimaryHandler(GetThumbprint(certificate));

        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseCookies);
        Assert.NotNull(handler.SslOptions.ClientCertificateContext);
    }

    [Fact]
    public async Task CreatePrimaryHandler_WithSelectedCertificate_PresentsItInTheTlsHandshake()
    {
        var (rsa, certificate) = CreateRsaCertificate();
        var key = KeyFactory.CreateRsa(new KeyMetadata { KeyId = "k1" }, rsa, certificate);
        var provider = CreateProvider(key);
        provider.SelectHttpClientName(CreateRegistration());
        Assert.True(
            provider.TryGetCertificate(GetThumbprint(certificate), out var clientCertificate)
        );

        var serverCertificate = Track(CreateServerCertificate());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var serverTask = Task.Run(
                async () =>
                {
                    using var tcpClient = await listener.AcceptTcpClientAsync(cts.Token);
                    await using var sslStream = new SslStream(tcpClient.GetStream());
                    await sslStream.AuthenticateAsServerAsync(
                        new SslServerAuthenticationOptions
                        {
                            ServerCertificate = serverCertificate,
                            ClientCertificateRequired = true,
#pragma warning disable CA5359 // hermetic loopback server with a throwaway self-signed certificate
                            RemoteCertificateValidationCallback = (_, _, _, _) => true,
#pragma warning restore CA5359
                        },
                        cts.Token
                    );

                    var captured = sslStream.RemoteCertificate?.GetRawCertData();

                    var buffer = new byte[1024];
                    var total = 0;
                    while (total < buffer.Length)
                    {
                        var read = await sslStream.ReadAsync(buffer.AsMemory(total), cts.Token);
                        if (read == 0)
                            break;
                        total += read;
                        if (buffer.AsSpan(0, total).IndexOf("\r\n\r\n"u8) >= 0)
                            break;
                    }

                    await sslStream.WriteAsync(
                        "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(),
                        cts.Token
                    );
                    await sslStream.FlushAsync(cts.Token);

                    return captured;
                },
                cts.Token
            );

            using var handler = provider.CreatePrimaryHandler(GetThumbprint(certificate));
#pragma warning disable CA5359 // hermetic loopback server with a throwaway self-signed certificate
            handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
#pragma warning restore CA5359
            using var httpClient = new HttpClient(handler);

            using var response = await httpClient.GetAsync($"https://127.0.0.1:{port}/", cts.Token);
            var captured = await serverTask;

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(captured);
            Assert.Equal(clientCertificate.RawData, captured);
        }
        finally
        {
            listener.Stop();
        }
    }

    private static X509Certificate2 CreateServerCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        using var selfSigned = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(30)
        );

        if (!OperatingSystem.IsWindows())
            return X509CertificateLoader.LoadPkcs12(
                selfSigned.Export(X509ContentType.Pkcs12),
                null
            );

        // SChannel cannot use an ephemeral private key for TLS.
        const string password = "test-password";
        return X509CertificateLoader.LoadPkcs12(
            selfSigned.Export(X509ContentType.Pkcs12, password),
            password,
            X509KeyStorageFlags.DefaultKeySet
        );
    }

    #endregion

    #region TryGetCertificate Tests

    [Fact]
    public void TryGetCertificate_WithUnknownThumbprint_ReturnsFalse()
    {
        var mockKeyRingProvider = CreateStrictMock<IClientKeyRingProvider>();
        var provider = Track(
            new ClientCertificateProvider(mockKeyRingProvider.Object, TimeProvider)
        );

        Assert.False(provider.TryGetCertificate("UNKNOWN", out var certificate));
        Assert.Null(certificate);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_WithCachedCertificate_ClearsTheCache()
    {
        var (rsa, certificate) = CreateRsaCertificate();
        var key = KeyFactory.CreateRsa(new KeyMetadata { KeyId = "k1" }, rsa, certificate);
        var provider = CreateProvider(key);
        provider.SelectHttpClientName(CreateRegistration());

        provider.Dispose();

        Assert.False(provider.TryGetCertificate(GetThumbprint(certificate), out _));
    }

    #endregion
}
