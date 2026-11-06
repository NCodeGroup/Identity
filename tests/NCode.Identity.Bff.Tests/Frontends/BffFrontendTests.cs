// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId;
using Xunit;

namespace NCode.Identity.Bff.Tests.Frontends;

public class BffFrontendTests : BaseTests
{
    private static BffFrontend CreateFrontend(string mode)
    {
        var options = new BffFrontendOptions
        {
            Authority = "https://op.example.com",
            ClientId = "bff",
            BackchannelLogoutSessionRequired = false,
            ClientAuthentication = new BffClientAuthenticationOptions { KeyRing = "client-keys" },
            DPoP = new BffDPoPOptions { Mode = mode, KeyRing = "dpop-keys" },
        };
        return new BffFrontend("web", options, null);
    }

    private static BffFrontendOptions CreateOptions() =>
        new() { Authority = "https://op.example.com", ClientId = "bff" };

    #region Registration Tests

    [Fact]
    public void Registration_Required_BindsDPoPAndOptions()
    {
        var frontend = CreateFrontend(BffDPoPModes.Required);

        var registration = frontend.Registration;

        Assert.Equal("https://op.example.com", registration.Authority);
        Assert.Equal("bff", registration.ClientId);
        Assert.Equal(
            OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt,
            registration.AuthenticationMethod
        );
        Assert.Equal("client-keys", registration.KeyRing);
        Assert.Equal("dpop-keys", registration.DPoPKeyRing);
        Assert.True(registration.DPoPBoundAccessTokens);
        Assert.False(registration.BackchannelLogoutSessionRequired);
        Assert.Same(registration, frontend.Registration);
    }

    [Fact]
    public void Registration_WhenSupported_DoesNotBindAccessTokens()
    {
        var frontend = CreateFrontend(BffDPoPModes.WhenSupported);

        var registration = frontend.Registration;

        Assert.Equal("dpop-keys", registration.DPoPKeyRing);
        Assert.False(registration.DPoPBoundAccessTokens);
    }

    [Fact]
    public void Registration_BindsTheIdTokenAndUserInfoProtection()
    {
        var options = new BffFrontendOptions
        {
            Authority = "https://op.example.com",
            ClientId = "bff",
            IdToken = new BffIdTokenOptions
            {
                SigningAlgorithms = ["PS256"],
                KeyManagementAlgorithms = ["RSA-OAEP-256"],
                ContentEncryptionAlgorithms = ["A256GCM"],
            },
            UserInfo = new BffUserInfoOptions
            {
                Load = true,
                SigningAlgorithms = ["ES256"],
                KeyManagementAlgorithms = ["ECDH-ES"],
                ContentEncryptionAlgorithms = ["A128GCM"],
            },
        };

        var registration = new BffFrontend("web", options, null).Registration;

        Assert.Equal(["PS256"], registration.IdTokenSigningAlgorithms);
        Assert.Equal(["RSA-OAEP-256"], registration.IdTokenKeyManagementAlgorithms);
        Assert.Equal(["A256GCM"], registration.IdTokenContentEncryptionAlgorithms);
        Assert.Equal(["ES256"], registration.UserInfoSigningAlgorithms);
        Assert.Equal(["ECDH-ES"], registration.UserInfoKeyManagementAlgorithms);
        Assert.Equal(["A128GCM"], registration.UserInfoContentEncryptionAlgorithms);
    }

    #endregion

    #region Cookie Name Tests

    [Fact]
    public void CookieNames_CarryTheFrontendName()
    {
        var frontend = new BffFrontend("web", CreateOptions(), null);

        Assert.Equal($"{BffDefaults.SessionCookieName}.web", frontend.SessionCookieName);
        Assert.Equal($"{BffDefaults.LoginCookieName}.web", frontend.LoginCookieName);
        Assert.Equal($"{BffDefaults.PageReloadCookieName}.web", frontend.PageReloadCookieName);
    }

    #endregion

    #region CreateSelection Tests

    [Fact]
    public void CreateSelection_WithoutRule_ReturnsNull()
    {
        var options = new BffFrontendOptions();

        var selection = BffFrontend.CreateSelection(options);

        Assert.Null(selection);
    }

    [Fact]
    public void CreateSelection_WithRule_ListsThePathPrefixThenTheRemoteApiPaths()
    {
        var options = new BffFrontendOptions
        {
            PublicOrigin = "https://app.example.com",
            PathPrefix = "/web/bff",
            Selection = new BffFrontendSelectionOptions
            {
                Rule = BffFrontendSelectorNames.Path,
                Values = ["/web"],
            },
            RemoteApis = new Dictionary<string, BffRemoteApiOptions>(
                StringComparer.OrdinalIgnoreCase
            )
            {
                ["orders"] = new() { Path = "/web/orders" },
                ["billing"] = new() { Path = "/web/billing" },
            },
        };

        var selection = BffFrontend.CreateSelection(options);

        Assert.NotNull(selection);
        Assert.Equal(["/web"], selection.Values);
        Assert.Equal("https://app.example.com", selection.PublicOrigin);
        Assert.Equal(["/web/bff", "/web/orders", "/web/billing"], selection.Paths);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_RuleWithoutSelector_Throws()
    {
        var options = new BffFrontendOptions
        {
            Selection = new BffFrontendSelectionOptions
            {
                Rule = BffFrontendSelectorNames.Host,
                Values = ["app.example.com"],
            },
        };

        Assert.Throws<ArgumentException>(() => new BffFrontend("web", options, null));
    }

    [Fact]
    public void Constructor_SelectorWithoutRule_Throws()
    {
        var selector = CreateStrictMock<IBffFrontendSelector>();

        Assert.Throws<ArgumentException>(() =>
            new BffFrontend("web", new BffFrontendOptions(), selector.Object)
        );
    }

    [Fact]
    public void Constructor_SelectorOfAnotherRule_Throws()
    {
        var options = new BffFrontendOptions
        {
            Selection = new BffFrontendSelectionOptions
            {
                Rule = BffFrontendSelectorNames.Host,
                Values = ["app.example.com"],
            },
        };
        var selector = CreateStrictMock<IBffFrontendSelector>();
        selector.Setup(x => x.Name).Returns(BffFrontendSelectorNames.Path).Verifiable();

        Assert.Throws<ArgumentException>(() => new BffFrontend("web", options, selector.Object));
    }

    #endregion

    #region IsMatch Tests

    [Fact]
    public void IsMatch_DelegatesToTheSelector()
    {
        var options = CreateOptions();
        options.Selection = new BffFrontendSelectionOptions
        {
            Rule = BffFrontendSelectorNames.Host,
            Values = ["app.example.com"],
        };
        var selector = CreateStrictMock<IBffFrontendSelector>();
        selector.Setup(x => x.Name).Returns(BffFrontendSelectorNames.Host).Verifiable();
        var request = new DefaultHttpContext().Request;
        selector
            .Setup(x => x.IsMatch(request, It.Is<BffFrontendSelection>(s => s.Values.Count == 1)))
            .Returns(true)
            .Verifiable();
        var frontend = new BffFrontend("web", options, selector.Object);

        var result = frontend.IsMatch(request);

        Assert.True(result);
        Assert.False(frontend.IsFallback);
    }

    [Fact]
    public void IsMatch_WithoutRule_ReturnsFalse()
    {
        var frontend = new BffFrontend("web", CreateOptions(), null);
        var request = new DefaultHttpContext().Request;

        var result = frontend.IsMatch(request);

        Assert.False(result);
        Assert.True(frontend.IsFallback);
    }

    #endregion
}
