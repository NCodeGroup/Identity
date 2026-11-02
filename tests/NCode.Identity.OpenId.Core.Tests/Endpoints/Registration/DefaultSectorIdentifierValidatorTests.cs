// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Moq;
using NCode.Buffers;
using NCode.Identity.OpenId.Authentication.Endpoints.Registration;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Registration;

public class DefaultSectorIdentifierValidatorTests : BaseTests
{
    #region Scaffold

    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }

    public DefaultSectorIdentifierValidatorTests()
    {
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
    }

    private DefaultSectorIdentifierValidator CreateValidator() =>
        new(MockHttpClientFactory.Object, new DefaultOutboundHttpContentReader());

    private (IOpenIdErrorFactory factory, Func<string?> capturedCode) CreateErrorFactory()
    {
        string? capturedCode = null;
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Callback<string>(code => capturedCode = code)
            .Returns(mockError.Object);
        return (mockErrorFactory.Object, () => capturedCode);
    }

    // An invalid URI is refused before any of the tenant's settings is read, and a host outside the allow-list before
    // the fetch limits are read.
    private OpenIdContext CreateContext(
        IOpenIdErrorFactory errorFactory,
        IReadOnlyCollection<string>? allowedHosts = null,
        int maxSizeBytes = 100 * 1024,
        TimeSpan? timeout = null,
        bool readsSettings = true,
        bool fetches = true
    )
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();

        var outHosts = allowedHosts;
        if (readsSettings)
        {
            mockSettings
                .Setup(x =>
                    x.TryGetValue(OpenIdSettingKeys.SectorIdentifierUriAllowedHosts, out outHosts)
                )
                .Returns(allowedHosts is not null)
                .Verifiable();
        }

        if (readsSettings && fetches)
        {
            mockSettings
                .Setup(x => x.GetValue(OpenIdSettingKeys.SectorIdentifierUriFetchTimeout))
                .Returns(timeout ?? TimeSpan.FromSeconds(10))
                .Verifiable();
            mockSettings
                .Setup(x => x.GetValue(OpenIdSettingKeys.SectorIdentifierUriMaxSizeBytes))
                .Returns(maxSizeBytes)
                .Verifiable();
        }

        var mockProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
        mockProvider.Setup(x => x.Collection).Returns(mockSettings.Object).Verifiable();

        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.Setup(x => x.SettingsProvider).Returns(mockProvider.Object).Verifiable();

        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockContext.Setup(x => x.ErrorFactory).Returns(errorFactory).Verifiable();
        return mockContext.Object;
    }

    private void SetupHttpResponse(HttpResponseMessage response) =>
        MockHttpClientFactory
            .Setup(x => x.CreateClient(GuardedHttpClient.Name))
            .Returns(new HttpClient(new StubHttpMessageHandler(response)))
            .Verifiable();

    private static HttpResponseMessage JsonResponse(string json)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, SecureEncoding.UTF8, "application/json"),
        };
        return response;
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(response);
    }

    #endregion

    #region ValidateAsync

    [Fact]
    public async Task ValidateAsync_NonHttpsUri_ReturnsInvalidClientMetadata()
    {
        var validator = CreateValidator();
        var (factory, capturedCode) = CreateErrorFactory();
        var context = CreateContext(factory, readsSettings: false);

        var error = await validator.ValidateAsync(
            context,
            "http://sector.example.com/uris.json",
            ["https://example.com/callback"],
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    [Fact]
    public async Task ValidateAsync_RelativeUri_ReturnsInvalidClientMetadata()
    {
        var validator = CreateValidator();
        var (factory, capturedCode) = CreateErrorFactory();
        var context = CreateContext(factory, readsSettings: false);

        var error = await validator.ValidateAsync(
            context,
            "/uris.json",
            ["https://example.com/callback"],
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    [Fact]
    public async Task ValidateAsync_HostNotInAllowList_ReturnsInvalidClientMetadata()
    {
        var validator = CreateValidator();
        var (factory, capturedCode) = CreateErrorFactory();
        var context = CreateContext(factory, allowedHosts: ["trusted.example.com"], fetches: false);

        var error = await validator.ValidateAsync(
            context,
            "https://sector.example.com/uris.json",
            ["https://example.com/callback"],
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    [Fact]
    public async Task ValidateAsync_DocumentListsAllRedirectUris_ReturnsNull()
    {
        var validator = CreateValidator();
        var (factory, _) = CreateErrorFactory();
        var context = CreateContext(factory, allowedHosts: ["sector.example.com"]);
        SetupHttpResponse(
            JsonResponse("[\"https://a.example.com/cb\", \"https://b.example.com/cb\"]")
        );

        var error = await validator.ValidateAsync(
            context,
            "https://sector.example.com/uris.json",
            ["https://a.example.com/cb", "https://b.example.com/cb"],
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateAsync_DocumentMissingRedirectUri_ReturnsInvalidClientMetadata()
    {
        var validator = CreateValidator();
        var (factory, capturedCode) = CreateErrorFactory();
        var context = CreateContext(factory);
        SetupHttpResponse(JsonResponse("[\"https://a.example.com/cb\"]"));

        var error = await validator.ValidateAsync(
            context,
            "https://sector.example.com/uris.json",
            ["https://a.example.com/cb", "https://b.example.com/cb"],
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    [Fact]
    public async Task ValidateAsync_NonArrayDocument_ReturnsInvalidClientMetadata()
    {
        var validator = CreateValidator();
        var (factory, capturedCode) = CreateErrorFactory();
        var context = CreateContext(factory);
        SetupHttpResponse(JsonResponse("{\"not\":\"an array\"}"));

        var error = await validator.ValidateAsync(
            context,
            "https://sector.example.com/uris.json",
            ["https://a.example.com/cb"],
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    [Fact]
    public async Task ValidateAsync_NonSuccessStatus_ReturnsInvalidClientMetadata()
    {
        var validator = CreateValidator();
        var (factory, capturedCode) = CreateErrorFactory();
        var context = CreateContext(factory);
        SetupHttpResponse(new HttpResponseMessage(HttpStatusCode.NotFound));

        var error = await validator.ValidateAsync(
            context,
            "https://sector.example.com/uris.json",
            ["https://a.example.com/cb"],
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    [Fact]
    public async Task ValidateAsync_OversizedContentLength_ReturnsInvalidClientMetadata()
    {
        var validator = CreateValidator();
        var (factory, capturedCode) = CreateErrorFactory();
        var context = CreateContext(factory, maxSizeBytes: 8);
        SetupHttpResponse(JsonResponse("[\"https://a.example.com/callback\"]"));

        var error = await validator.ValidateAsync(
            context,
            "https://sector.example.com/uris.json",
            ["https://a.example.com/callback"],
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    #endregion
}
