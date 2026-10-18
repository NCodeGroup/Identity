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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Sessions;

public class DefaultSessionStateServiceTests : BaseTests
{
    private const string ClientId = "client-1";
    private const string Digest = "digest-value";
    private const string Salt = "salt-value";

    private Mock<ICryptoService> MockCryptoService { get; }
    private DefaultSessionStateService Service { get; }

    public DefaultSessionStateServiceTests()
    {
        MockCryptoService = CreateStrictMock<ICryptoService>();
        Service = new DefaultSessionStateService(MockCryptoService.Object);
    }

    #region Scaffolding

    private Mock<OpenIdContext> CreateContext(DefaultHttpContext httpContext)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.SetupGet(x => x.Http).Returns(httpContext);
        return mockContext;
    }

    private void SetupSalt() =>
        MockCryptoService.Setup(x => x.GenerateKey(16, BinaryEncodingType.Base64Url)).Returns(Salt);

    private void SetupDigest(string expectedInput) =>
        MockCryptoService
            .Setup(x =>
                x.HashValue(
                    expectedInput,
                    HashAlgorithmType.Sha256,
                    BinaryEncodingType.Base64Url,
                    null
                )
            )
            .Returns(Digest)
            .Verifiable();

    #endregion

    [Fact]
    public void CreateSessionState_WhenNoBrowserStateCookie_SetsCookieAndReturnsState()
    {
        var httpContext = new DefaultHttpContext();
        var mockContext = CreateContext(httpContext);

        const string browserState = "browser-state";
        MockCryptoService
            .Setup(x => x.GenerateKey(32, BinaryEncodingType.Base64Url))
            .Returns(browserState);
        SetupSalt();
        SetupDigest($"{ClientId} https://rp.example {browserState} {Salt}");

        var result = Service.CreateSessionState(
            mockContext.Object,
            ClientId,
            new Uri("https://rp.example/callback")
        );

        Assert.Equal($"{Digest}.{Salt}", result);

        var setCookie = httpContext.Response.Headers.SetCookie.ToString();
        Assert.Contains(
            $"{DefaultSessionStateService.BrowserStateCookieName}={browserState}",
            setCookie
        );
        Assert.Contains("samesite=none", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateSessionState_WhenBrowserStateCookieExists_ReusesItWithoutSettingCookie()
    {
        var httpContext = new DefaultHttpContext();
        const string browserState = "existing-state";
        httpContext.Request.Headers.Cookie =
            $"{DefaultSessionStateService.BrowserStateCookieName}={browserState}";
        var mockContext = CreateContext(httpContext);

        SetupSalt();
        SetupDigest($"{ClientId} https://rp.example {browserState} {Salt}");

        var result = Service.CreateSessionState(
            mockContext.Object,
            ClientId,
            new Uri("https://rp.example/callback")
        );

        Assert.Equal($"{Digest}.{Salt}", result);
        Assert.True(StringValues.IsNullOrEmpty(httpContext.Response.Headers.SetCookie));
    }

    [Fact]
    public void ClearBrowserState_DeletesCookie()
    {
        var httpContext = new DefaultHttpContext();
        var mockContext = CreateContext(httpContext);

        Service.ClearBrowserState(mockContext.Object);

        var setCookie = httpContext.Response.Headers.SetCookie.ToString();
        Assert.Contains($"{DefaultSessionStateService.BrowserStateCookieName}=", setCookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", setCookie, StringComparison.OrdinalIgnoreCase);
    }
}
