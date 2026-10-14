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
using System.Text;
using Moq;
using NCode.Identity.OpenId.Authentication.Endpoints.Registration.Logic;
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

    private DefaultSectorIdentifierValidator CreateValidator() => new(MockHttpClientFactory.Object);

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

    private OpenIdContext CreateContext(
        IOpenIdErrorFactory errorFactory,
        IReadOnlyCollection<string>? allowedHosts = null,
        int maxSizeBytes = 100 * 1024,
        TimeSpan? timeout = null
    )
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();

        var outHosts = allowedHosts;
        mockSettings
            .Setup(x =>
                x.TryGetValue(OpenIdSettingKeys.SectorIdentifierUriAllowedHosts, out outHosts)
            )
            .Returns(allowedHosts is not null);
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.SectorIdentifierUriFetchTimeout))
            .Returns(timeout ?? TimeSpan.FromSeconds(10));
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.SectorIdentifierUriMaxSizeBytes))
            .Returns(maxSizeBytes);

        var mockProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
        mockProvider.Setup(x => x.Collection).Returns(mockSettings.Object);

        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.Setup(x => x.SettingsProvider).Returns(mockProvider.Object);

        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object);
        mockContext.Setup(x => x.ErrorFactory).Returns(errorFactory);
        return mockContext.Object;
    }

    private void SetupHttpResponse(HttpResponseMessage response) =>
        MockHttpClientFactory
            .Setup(x => x.CreateClient(DefaultSectorIdentifierValidator.HttpClientName))
            .Returns(new HttpClient(new StubHttpMessageHandler(response)));

    private static HttpResponseMessage JsonResponse(string json)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
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
        var context = CreateContext(factory);

        var error = await validator.ValidateAsync(
            context,
            "http://sector.example.com/uris.json",
            ["https://example.com/callback"],
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    [Fact]
    public async Task ValidateAsync_RelativeUri_ReturnsInvalidClientMetadata()
    {
        var validator = CreateValidator();
        var (factory, capturedCode) = CreateErrorFactory();
        var context = CreateContext(factory);

        var error = await validator.ValidateAsync(
            context,
            "/uris.json",
            ["https://example.com/callback"],
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    [Fact]
    public async Task ValidateAsync_HostNotInAllowList_ReturnsInvalidClientMetadata()
    {
        var validator = CreateValidator();
        var (factory, capturedCode) = CreateErrorFactory();
        var context = CreateContext(factory, allowedHosts: ["trusted.example.com"]);

        var error = await validator.ValidateAsync(
            context,
            "https://sector.example.com/uris.json",
            ["https://example.com/callback"],
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClientMetadata, capturedCode());
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
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClientMetadata, capturedCode());
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
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClientMetadata, capturedCode());
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
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClientMetadata, capturedCode());
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
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    #endregion
}
