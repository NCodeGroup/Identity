// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Buffers;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId.Client;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using Xunit;

namespace NCode.Identity.Bff.Tests.Endpoints;

public sealed class BffJwksHandlerTests : BaseTests
{
    private const string KeyRingName = "client-keys";

    private static readonly string[] PrivateParameters =
    [
        "d",
        "p",
        "q",
        "dp",
        "dq",
        "qi",
        "oth",
        "k",
    ];

    private ServiceProvider ServiceProvider { get; }
    private ECDsa Ec { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private RSA Rsa { get; } = RSA.Create(2048);
    private SecretKey EcKey { get; }
    private SecretKey RsaKey { get; }
    private SecretKey SymmetricKey { get; }

    public BffJwksHandlerTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        var factory = ServiceProvider.GetRequiredService<ISecretKeyFactory>();
        EcKey = factory.CreateEcc(new KeyMetadata { KeyId = "ec-1" }, Ec);
        RsaKey = factory.CreateRsa(new KeyMetadata { KeyId = "rsa-1" }, Rsa);
        SymmetricKey = factory.CreateSymmetric(
            new KeyMetadata { KeyId = "shared-1" },
            RandomNumberGenerator.GetBytes(32)
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Ec.Dispose();
            Rsa.Dispose();
            ServiceProvider.Dispose();
        }

        base.Dispose(disposing);
    }

    private Mock<IClientKeyRingProvider> CreateKeyRingProvider(params SecretKey[] keys)
    {
        var collection = ServiceProvider
            .GetRequiredService<ISecretKeyCollectionFactory>()
            .Create(keys);
        var mockKeyRingProvider = CreateStrictMock<IClientKeyRingProvider>();
        mockKeyRingProvider.Setup(x => x.GetKeyRing(KeyRingName)).Returns(collection).Verifiable();
        return mockKeyRingProvider;
    }

    private BffJwksHandler CreateHandler(IClientKeyRingProvider keyRingProvider, string? keyRing) =>
        new(
            keyRingProvider,
            ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>(),
            Microsoft.Extensions.Options.Options.Create(
                new BffOptions
                {
                    Jwks = { Enabled = true },
                    ClientAuthentication = { KeyRing = keyRing },
                }
            )
        );

    private static DefaultHttpContext CreateHttpContext() =>
        new() { Response = { Body = new MemoryStream() } };

    private static async Task<JsonNode> ReadBodyAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, SecureEncoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return JsonNode.Parse(body) ?? throw new InvalidOperationException("The body is empty.");
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_PublishesThePublicKeysOfTheKeyRing()
    {
        var mockKeyRingProvider = CreateKeyRingProvider(EcKey, RsaKey);
        var handler = CreateHandler(mockKeyRingProvider.Object, KeyRingName);
        var httpContext = CreateHttpContext();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", httpContext.Response.ContentType);
        Assert.Contains("no-store", httpContext.Response.Headers.CacheControl.ToString());

        // A key set has no order, and the key ring does not promise one.
        var keys = (await ReadBodyAsync(httpContext))["keys"]!
            .AsArray()
            .Select(x => (Kid: x!["kid"]!.GetValue<string>(), Kty: x!["kty"]!.GetValue<string>()))
            .OrderBy(x => x.Kid, StringComparer.Ordinal);
        Assert.Equal<(string, string)>([("ec-1", "EC"), ("rsa-1", "RSA")], keys);
    }

    [Fact]
    public async Task HandleAsync_NeverPublishesPrivateParameters()
    {
        var mockKeyRingProvider = CreateKeyRingProvider(EcKey, RsaKey);
        var handler = CreateHandler(mockKeyRingProvider.Object, KeyRingName);
        var httpContext = CreateHttpContext();

        await handler.HandleAsync(httpContext);

        var keys = (await ReadBodyAsync(httpContext))["keys"]!.AsArray();
        Assert.All(
            keys,
            key => Assert.DoesNotContain(key!.AsObject(), x => PrivateParameters.Contains(x.Key))
        );
    }

    [Fact]
    public async Task HandleAsync_OmitsKeysWithNoPublicPart()
    {
        var mockKeyRingProvider = CreateKeyRingProvider(SymmetricKey, EcKey);
        var handler = CreateHandler(mockKeyRingProvider.Object, KeyRingName);
        var httpContext = CreateHttpContext();

        await handler.HandleAsync(httpContext);

        var keys = (await ReadBodyAsync(httpContext))["keys"]!.AsArray();
        var key = Assert.Single(keys);
        Assert.Equal("ec-1", key!["kid"]!.GetValue<string>());
    }

    [Fact]
    public async Task HandleAsync_WhenKeyRingHasNoPublicKeys_PublishesAnEmptySet()
    {
        var mockKeyRingProvider = CreateKeyRingProvider(SymmetricKey);
        var handler = CreateHandler(mockKeyRingProvider.Object, KeyRingName);
        var httpContext = CreateHttpContext();

        await handler.HandleAsync(httpContext);

        var keys = (await ReadBodyAsync(httpContext))["keys"]!.AsArray();
        Assert.Empty(keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task HandleAsync_WhenKeyRingBlank_Throws(string? keyRing)
    {
        var mockKeyRingProvider = CreateStrictMock<IClientKeyRingProvider>();
        var handler = CreateHandler(mockKeyRingProvider.Object, keyRing);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(CreateHttpContext())
        );

        Assert.Contains("Bff:ClientAuthentication:KeyRing", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_WhenHttpContextNull_Throws()
    {
        var mockKeyRingProvider = CreateStrictMock<IClientKeyRingProvider>();
        var handler = CreateHandler(mockKeyRingProvider.Object, KeyRingName);

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.HandleAsync(null!));
    }

    #endregion
}
