// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Tests.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.Options;

public class BffOptionsValidatorTests : BaseTests
{
    private BffOptionsValidator CreateValidator(string environmentName = "Production")
    {
        var mockEnvironment = CreateStrictMock<IHostEnvironment>();
        mockEnvironment.Setup(x => x.EnvironmentName).Returns(environmentName).Verifiable();
        return new BffOptionsValidator(
            mockEnvironment.Object,
            [new HostBffFrontendSelector(), new PathBffFrontendSelector()]
        );
    }

    private const string WebFrontend = "web";

    private static BffOptions CreateValidOptions() =>
        new()
        {
            Frontends =
            {
                [WebFrontend] = new BffFrontendOptions
                {
                    Authority = "https://op.example.com",
                    PublicOrigin = "https://app.example.com",
                    ClientId = "bff",
                    ReturnUrls = ["/"],
                    TrustedOrigins = ["https://app.example.com"],
                },
            },
        };

    private static BffFrontendOptions Frontend(BffOptions options) =>
        options.Frontends[WebFrontend];

    #region Validate Tests

    [Fact]
    public void Validate_WhenValidBaseline_Succeeds()
    {
        var result = CreateValidator().Validate(null, CreateValidOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("op.example.com")]
    [InlineData("http://op.example.com")]
    [InlineData("https://op .example.com")]
    public void Validate_WhenAuthorityInvalid_Fails(string authority)
    {
        var options = CreateValidOptions();
        Frontend(options).Authority = authority;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Frontends:web:Authority"));
    }

    [Fact]
    public void Validate_WhenHttpAuthorityInDevelopment_Succeeds()
    {
        var options = CreateValidOptions();
        Frontend(options).Authority = "http://localhost:5000";

        var result = CreateValidator("Development").Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("app.example.com")]
    [InlineData("http://app.example.com")]
    [InlineData("https://app.example.com/")]
    [InlineData("https://app.example.com/app?x=1")]
    [InlineData("https://app.example.com#top")]
    [InlineData("https://user@app.example.com")]
    [InlineData("https://APP.example.com")]
    public void Validate_WhenPublicOriginInvalid_Fails(string publicOrigin)
    {
        var options = CreateValidOptions();
        Frontend(options).PublicOrigin = publicOrigin;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Frontends:web:PublicOrigin"));
    }

    [Fact]
    public void Validate_WhenPublicOriginInvalid_NamesTheValue()
    {
        var options = CreateValidOptions();
        Frontend(options).PublicOrigin = "https://app.example.com/";

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(
            result.Failures ?? [],
            x =>
                x.StartsWith(
                    "Bff:Frontends:web:PublicOrigin 'https://app.example.com/' must",
                    StringComparison.Ordinal
                )
        );
    }

    [Fact]
    public void Validate_WhenPublicOriginHasUserInfo_WithholdsTheValue()
    {
        var options = CreateValidOptions();
        Frontend(options).PublicOrigin = "https://user:secret@app.example.com";

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Frontends:web:PublicOrigin"));
        Assert.DoesNotContain(result.Failures ?? [], x => x.Contains("secret"));
    }

    [Fact]
    public void Validate_WhenAuthorityInvalid_NamesTheValue()
    {
        var options = CreateValidOptions();
        Frontend(options).Authority = "http://op.example.com";

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(
            result.Failures ?? [],
            x =>
                x.StartsWith(
                    "Bff:Frontends:web:Authority 'http://op.example.com' must",
                    StringComparison.Ordinal
                )
        );
    }

    [Fact]
    public void Validate_WhenAuthorityHasUserInfo_WithholdsTheValue()
    {
        var options = CreateValidOptions();
        Frontend(options).Authority = "http://user:secret@op.example.com";

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Frontends:web:Authority"));
        Assert.DoesNotContain(result.Failures ?? [], x => x.Contains("secret"));
    }

    [Fact]
    public void Validate_WhenPublicOriginHasAPathBase_Succeeds()
    {
        var options = CreateValidOptions();
        Frontend(options).PublicOrigin = "https://example.com/app";

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenHttpPublicOriginInDevelopment_Succeeds()
    {
        var options = CreateValidOptions();
        Frontend(options).PublicOrigin = "http://localhost:5000";

        var result = CreateValidator("Development").Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenClientIdBlank_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).ClientId = " ";

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Frontends:web:ClientId"));
    }

    [Fact]
    public void Validate_WhenClientAuthenticationMethodBlank_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).ClientAuthentication.Method = "";

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(
            result.Failures ?? [],
            x => x.Contains("Bff:Frontends:web:ClientAuthentication:Method")
        );
    }

    [Fact]
    public void Validate_WhenReturnUrlsEmpty_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).ReturnUrls = [];

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Frontends:web:ReturnUrls"));
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("//evil.com")]
    [InlineData("/orders?x=1")]
    public void Validate_WhenReturnUrlEntryInvalid_Fails(string entry)
    {
        var options = CreateValidOptions();
        Frontend(options).ReturnUrls = [entry];

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Frontends:web:ReturnUrls"));
    }

    [Theory]
    [InlineData("error")]
    [InlineData("//evil.com")]
    [InlineData("")]
    public void Validate_WhenErrorPathInvalid_Fails(string errorPath)
    {
        var options = CreateValidOptions();
        Frontend(options).ErrorPath = errorPath;

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Frontends:web:ErrorPath"));
    }

    [Theory]
    [InlineData("bff")]
    [InlineData("/bff/")]
    [InlineData("/")]
    [InlineData("/a/../b")]
    public void Validate_WhenPathPrefixInvalid_Fails(string pathPrefix)
    {
        var options = CreateValidOptions();
        Frontend(options).PathPrefix = pathPrefix;

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Frontends:web:PathPrefix"));
    }

    [Theory]
    [InlineData("https://app.example.com/")]
    [InlineData("https://app.example.com/path")]
    [InlineData("http://app.example.com")]
    [InlineData("app.example.com")]
    public void Validate_WhenTrustedOriginInvalid_Fails(string origin)
    {
        var options = CreateValidOptions();
        Frontend(options).TrustedOrigins = [origin];

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Frontends:web:TrustedOrigins"));
    }

    [Fact]
    public void Validate_WhenHttpTrustedOriginInDevelopment_Succeeds()
    {
        var options = CreateValidOptions();
        Frontend(options).TrustedOrigins = ["http://localhost:3000"];

        var result = CreateValidator("Development").Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(SameSiteMode.None)]
    [InlineData(SameSiteMode.Unspecified)]
    [InlineData((SameSiteMode)99)]
    public void Validate_WhenSameSiteInvalid_Fails(SameSiteMode sameSite)
    {
        var options = CreateValidOptions();
        Frontend(options).Session.SameSite = sameSite;

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(
            result.Failures ?? [],
            x => x.Contains("Bff:Frontends:web:Session:SameSite")
        );
    }

    [Fact]
    public void Validate_WhenIdleTimeoutNotPositive_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).Session.IdleTimeout = TimeSpan.Zero;

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(
            result.Failures ?? [],
            x => x.Contains("Bff:Frontends:web:Session:IdleTimeout")
        );
    }

    [Fact]
    public void Validate_WhenAbsoluteLifetimeBelowIdleTimeout_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).Session.IdleTimeout = TimeSpan.FromHours(2);
        Frontend(options).Session.AbsoluteLifetime = TimeSpan.FromHours(1);

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(
            result.Failures ?? [],
            x => x.Contains("Bff:Frontends:web:Session:AbsoluteLifetime")
        );
    }

    [Theory]
    [InlineData(30)]
    [InlineData(31 * 60)]
    public void Validate_WhenLoginLifetimeOutOfRange_Fails(int seconds)
    {
        var options = CreateValidOptions();
        Frontend(options).Session.LoginLifetime = TimeSpan.FromSeconds(seconds);

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(
            result.Failures ?? [],
            x => x.Contains("Bff:Frontends:web:Session:LoginLifetime")
        );
    }

    [Fact]
    public void Validate_WhenDPoPModeUnknown_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).DPoP.Mode = "Required";

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Frontends:web:DPoP:Mode"));
    }

    [Fact]
    public void Validate_WhenDPoPRequiredWithoutKeyRing_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).DPoP.Mode = BffDPoPModes.Required;
        Frontend(options).DPoP.KeyRing = " ";

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Frontends:web:DPoP:KeyRing"));
    }

    [Fact]
    public void Validate_WhenDPoPRequiredWithKeyRing_Succeeds()
    {
        var options = CreateValidOptions();
        Frontend(options).DPoP.Mode = BffDPoPModes.Required;
        Frontend(options).DPoP.KeyRing = "dpop";

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenIdTokenAndUserInfoProtectionIsConsistent_Succeeds()
    {
        var options = CreateValidOptions();
        Frontend(options).ClientAuthentication.KeyRing = "client-keys";
        Frontend(options).IdToken.SigningAlgorithms = ["PS256"];
        Frontend(options).IdToken.KeyManagementAlgorithms = ["RSA-OAEP-256"];
        Frontend(options).UserInfo.Load = true;
        Frontend(options).UserInfo.SigningAlgorithms = ["PS256"];
        Frontend(options).UserInfo.KeyManagementAlgorithms = ["RSA-OAEP-256"];
        Frontend(options).UserInfo.ContentEncryptionAlgorithms = ["A256GCM"];

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(" ", "must not contain a blank entry")]
    [InlineData("none", "must not contain 'none'")]
    [InlineData("PS256", "must not contain 'PS256' more than once")]
    public void Validate_WhenAnIdTokenAlgorithmListIsInvalid_Fails(string entry, string message)
    {
        var options = CreateValidOptions();
        Frontend(options).IdToken.SigningAlgorithms = ["PS256", entry];

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(
            result.Failures ?? [],
            x =>
                x.StartsWith(
                    "Bff:Frontends:web:IdToken:SigningAlgorithms",
                    StringComparison.Ordinal
                ) && x.Contains(message, StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Validate_WhenUserInfoContentEncryptionHasNoKeyManagement_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).UserInfo.SigningAlgorithms = ["PS256"];
        Frontend(options).UserInfo.ContentEncryptionAlgorithms = ["A256GCM"];

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(
            result.Failures ?? [],
            x =>
                x
                == "Bff:Frontends:web:UserInfo:ContentEncryptionAlgorithms requires Bff:Frontends:web:UserInfo:KeyManagementAlgorithms."
        );
    }

    [Fact]
    public void Validate_WhenUserInfoIsEncryptedButNotSigned_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).ClientAuthentication.KeyRing = "client-keys";
        Frontend(options).UserInfo.KeyManagementAlgorithms = ["RSA-OAEP-256"];

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(
            result.Failures ?? [],
            x =>
                x.StartsWith(
                    "Bff:Frontends:web:UserInfo:KeyManagementAlgorithms requires Bff:Frontends:web:UserInfo:SigningAlgorithms",
                    StringComparison.Ordinal
                )
        );
    }

    [Fact]
    public void Validate_WhenIdTokenEncryptionHasNoKeyRing_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).IdToken.KeyManagementAlgorithms = ["RSA-OAEP-256"];

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(
            result.Failures ?? [],
            x =>
                x.StartsWith(
                    "Bff:Frontends:web:ClientAuthentication:KeyRing",
                    StringComparison.Ordinal
                )
                && x.Contains(
                    "Bff:Frontends:web:IdToken:KeyManagementAlgorithms",
                    StringComparison.Ordinal
                )
        );
    }

    [Fact]
    public void Validate_WhenTheIdTokenAndUserInfoSectionsAreMissing_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).IdToken = null!;
        Frontend(options).UserInfo = null!;

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x == "Bff:Frontends:web:IdToken must be set.");
        Assert.Contains(result.Failures ?? [], x => x == "Bff:Frontends:web:UserInfo must be set.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3601)]
    public void Validate_WhenRefreshBeforeExpiryOutOfRange_Fails(int seconds)
    {
        var options = CreateValidOptions();
        options.Tokens.RefreshBeforeExpiry = TimeSpan.FromSeconds(seconds);

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Tokens:RefreshBeforeExpiry"));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(301)]
    public void Validate_WhenRefreshLeaseDurationOutOfRange_Fails(int seconds)
    {
        var options = CreateValidOptions();
        options.Tokens.RefreshLeaseDuration = TimeSpan.FromSeconds(seconds);

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Tokens:RefreshLeaseDuration"));
    }

    [Fact]
    public void Validate_WhenAntiforgeryHeaderNameBlank_Fails()
    {
        var options = CreateValidOptions();
        options.Antiforgery.HeaderName = "";

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Antiforgery:HeaderName"));
    }

    [Theory]
    [InlineData("content-type")]
    [InlineData("Accept")]
    [InlineData("Authorization")]
    [InlineData("DPOP")]
    [InlineData("Cookie")]
    public void Validate_WhenAntiforgeryHeaderNameForbidden_Fails(string headerName)
    {
        var options = CreateValidOptions();
        options.Antiforgery.HeaderName = headerName;

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Antiforgery:HeaderName"));
    }

    [Fact]
    public void Validate_WhenAntiforgeryHeaderValueBlank_Fails()
    {
        var options = CreateValidOptions();
        options.Antiforgery.HeaderValue = " ";

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Antiforgery:HeaderValue"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("default-src 'self'\r\nSet-Cookie: x=1")]
    [InlineData("default-src 'sélf'")]
    public void Validate_WhenSecurityHeadersContentSecurityPolicyInvalid_Fails(string value)
    {
        var options = CreateValidOptions();
        Frontend(options).SecurityHeaders.ContentSecurityPolicy = value;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures ?? [],
            x => x.Contains("Bff:Frontends:web:SecurityHeaders:ContentSecurityPolicy")
        );
    }

    [Fact]
    public void Validate_WhenSecurityHeadersReferrerPolicyBlank_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).SecurityHeaders.ReferrerPolicy = "";

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures ?? [],
            x => x.Contains("Bff:Frontends:web:SecurityHeaders:ReferrerPolicy")
        );
    }

    [Fact]
    public void Validate_WhenSecurityHeadersCrossOriginOpenerPolicyHasControlCharacter_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).SecurityHeaders.CrossOriginOpenerPolicy = "same-origin\t";

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures ?? [],
            x => x.Contains("Bff:Frontends:web:SecurityHeaders:CrossOriginOpenerPolicy")
        );
    }

    [Fact]
    public void Validate_WhenJwksEnabledWithoutKeyRing_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).Jwks.Enabled = true;
        Frontend(options).ClientAuthentication.KeyRing = " ";

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures ?? [],
            x =>
                x.Contains("Bff:Frontends:web:Jwks:Enabled")
                && x.Contains("Bff:Frontends:web:ClientAuthentication:KeyRing")
        );
    }

    [Fact]
    public void Validate_WhenJwksEnabledWithKeyRing_Succeeds()
    {
        var options = CreateValidOptions();
        Frontend(options).Jwks.Enabled = true;
        Frontend(options).ClientAuthentication.KeyRing = "client-keys";

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenJwksNull_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).Jwks = null!;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures ?? [],
            x => x.Contains("Bff:Frontends:web:Jwks must be set.")
        );
    }

    [Fact]
    public void Validate_WhenSecurityHeadersNull_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).SecurityHeaders = null!;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures ?? [],
            x => x.Contains("Bff:Frontends:web:SecurityHeaders must be set")
        );
    }

    [Theory]
    [InlineData("iss")]
    [InlineData("sid")]
    [InlineData("nonce")]
    [InlineData("at_hash")]
    [InlineData("c_hash")]
    [InlineData("s_hash")]
    [InlineData("aud")]
    [InlineData("azp")]
    [InlineData("exp")]
    [InlineData("iat")]
    [InlineData("nbf")]
    public void Validate_WhenUserClaimReserved_Fails(string claim)
    {
        var options = CreateValidOptions();
        Frontend(options).UserClaims = [claim];

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Frontends:web:UserClaims"));
    }

    [Fact]
    public void Validate_WhenUserClaimAllowed_Succeeds()
    {
        var options = CreateValidOptions();
        Frontend(options).UserClaims = ["email"];

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("state")]
    [InlineData("redirect_uri")]
    [InlineData("Prompt")]
    public void Validate_WhenLoginParameterNotAllowed_Fails(string parameter)
    {
        var options = CreateValidOptions();
        Frontend(options).LoginParameters = [parameter];

        var result = CreateValidator().Validate(null, options);

        Assert.Contains(
            result.Failures ?? [],
            x => x.Contains("Bff:Frontends:web:LoginParameters")
        );
    }

    [Fact]
    public void Validate_WhenLoginParametersAllowed_Succeeds()
    {
        var options = CreateValidOptions();
        Frontend(options).LoginParameters =
        [
            "prompt",
            "max_age",
            "acr_values",
            "login_hint",
            "ui_locales",
        ];

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenRemoteApisValid_Succeeds()
    {
        var options = CreateValidOptions();
        Frontend(options).RemoteApis["orders"] = new BffRemoteApiOptions
        {
            Path = "/api/orders",
            Destination = "https://api.example.com/v1",
        };
        Frontend(options).RemoteApis["customers"] = new BffRemoteApiOptions
        {
            Path = "/api/customers",
            Destination = "https://api.example.com/v1",
        };

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenRemoteApisNull_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).RemoteApis = null!;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Bff:Frontends:web:RemoteApis must be set.", result.Failures ?? []);
    }

    [Fact]
    public void Validate_WhenRemoteApiInvalid_FailsWithItsKey()
    {
        var options = CreateValidOptions();
        Frontend(options).RemoteApis["orders"] = new BffRemoteApiOptions
        {
            Path = "/api/orders",
            Destination = "http://api.example.com",
        };

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures ?? [],
            x =>
                x.StartsWith(
                    "Bff:Frontends:web:RemoteApis:orders:Destination",
                    StringComparison.Ordinal
                )
        );
    }

    [Fact]
    public void Validate_WhenRemoteApiEntryNull_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).RemoteApis["orders"] = null!;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Bff:Frontends:web:RemoteApis:orders must be set.", result.Failures ?? []);
    }

    [Fact]
    public void Validate_WhenRemoteApiOverlapsThePathPrefix_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).RemoteApis["orders"] = new BffRemoteApiOptions
        {
            Path = "/bff/orders",
            Destination = "https://api.example.com",
        };

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "Bff:Frontends:web:RemoteApis:orders:Path '/bff/orders' must not overlap Bff:Frontends:web:PathPrefix '/bff'.",
            result.Failures ?? []
        );
    }

    [Fact]
    public void Validate_WhenRemoteApisOverlap_Fails()
    {
        var options = CreateValidOptions();
        Frontend(options).RemoteApis["api"] = new BffRemoteApiOptions
        {
            Path = "/api",
            Destination = "https://api.example.com",
        };
        Frontend(options).RemoteApis["orders"] = new BffRemoteApiOptions
        {
            Path = "/api/orders",
            Destination = "https://api.example.com",
        };

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "Bff:Frontends:web:RemoteApis:orders:Path '/api/orders' must not overlap Bff:Frontends:web:RemoteApis:api:Path '/api'.",
            result.Failures ?? []
        );
    }

    #endregion

    #region Frontends Tests

    [Fact]
    public void Validate_WhenFrontendValid_Succeeds()
    {
        var options = CreateValidOptions();
        options.Frontends["web"] = BffTestFrontends.CreateOptions();

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenNoFrontend_Fails()
    {
        var options = CreateValidOptions();
        options.Frontends.Clear();

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "Bff:Frontends must configure at least one frontend.",
            result.Failures ?? []
        );
    }

    [Fact]
    public void Validate_WhenFrontendsNull_Fails()
    {
        var options = CreateValidOptions();
        options.Frontends = null!;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Bff:Frontends must be set.", result.Failures ?? []);
    }

    [Theory]
    [InlineData("Web")]
    [InlineData("-web")]
    [InlineData("web-")]
    [InlineData("a_b")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Validate_WhenFrontendNameInvalid_Fails(string name)
    {
        var options = CreateValidOptions();
        options.Frontends[name] = BffTestFrontends.CreateOptions();

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            $"Bff:Frontends key '{name}' must be 1 to 63 lowercase letters, digits and hyphens that start and end with a letter or digit.",
            result.Failures ?? []
        );
    }

    [Fact]
    public void Validate_WhenFrontendClientIdBlank_Fails()
    {
        var options = CreateValidOptions();
        options.Frontends["web"] = BffTestFrontends.CreateOptions(x => x.ClientId = "");

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Bff:Frontends:web:ClientId must not be blank.", result.Failures ?? []);
    }

    [Fact]
    public void Validate_WhenFrontendNull_Fails()
    {
        var options = CreateValidOptions();
        options.Frontends["web"] = null!;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Bff:Frontends:web must be set.", result.Failures ?? []);
    }

    [Fact]
    public void Validate_WhenTwoFrontendsHaveNoRule_Fails()
    {
        var options = CreateValidOptions();
        options.Frontends["admin"] = BffTestFrontends.CreateOptions(x =>
            x.PublicOrigin = "https://admin.example.com"
        );
        options.Frontends["web"] = BffTestFrontends.CreateOptions();

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "At most one entry of Bff:Frontends may have no Selection:Rule; it serves the requests no other frontend selects.",
            result.Failures ?? []
        );
    }

    [Fact]
    public void Validate_WhenFrontendRuleUnknown_Fails()
    {
        var options = CreateValidOptions();
        options.Frontends["web"] = BffTestFrontends.CreateOptions(x => x.Selection.Rule = "other");

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "Bff:Frontends:web:Selection:Rule 'other' is not a registered frontend selection rule.",
            result.Failures ?? []
        );
    }

    [Fact]
    public void Validate_WhenHostRuleHasNoValues_Fails()
    {
        var options = CreateValidOptions();
        options.Frontends["shop"] = BffTestFrontends.CreateOptions(x =>
        {
            x.PublicOrigin = "https://shop.example.com";
            x.Selection.Rule = BffFrontendSelectorNames.Host;
        });

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "Bff:Frontends:shop:Selection: Values must name at least one host.",
            result.Failures ?? []
        );
    }

    [Fact]
    public void Validate_WhenPathFrontendsOverlap_Fails()
    {
        var options = CreateValidOptions();
        options.Frontends["admin"] = BffTestFrontends.CreateOptions(x =>
        {
            x.PathPrefix = "/admin/bff";
            x.Selection.Rule = BffFrontendSelectorNames.Path;
            x.Selection.Values = ["/admin"];
        });
        options.Frontends["reports"] = BffTestFrontends.CreateOptions(x =>
        {
            x.PathPrefix = "/admin/reports/bff";
            x.Selection.Rule = BffFrontendSelectorNames.Path;
            x.Selection.Values = ["/admin/reports"];
        });

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "Bff:Frontends:admin:Selection and Bff:Frontends:reports:Selection select the same requests.",
            result.Failures ?? []
        );
    }

    [Fact]
    public void Validate_WhenFrontendsShareTheCallback_Fails()
    {
        var options = CreateValidOptions();
        options.Frontends["web"] = BffTestFrontends.CreateOptions();
        options.Frontends["shop"] = BffTestFrontends.CreateOptions(x =>
        {
            x.Selection.Rule = BffFrontendSelectorNames.Host;
            x.Selection.Values = ["app.example.com"];
        });

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "Bff:Frontends:shop and Bff:Frontends:web have the same PublicOrigin and PathPrefix; each frontend needs its own callback URI.",
            result.Failures ?? []
        );
    }

    #endregion
}
