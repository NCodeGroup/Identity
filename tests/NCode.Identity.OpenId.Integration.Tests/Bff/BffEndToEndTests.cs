// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Bff;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.RemoteKeys;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Bff;

/// <summary>
/// Proves the backend-for-frontend (BFF) against the NCode OpenID provider hosted in-process: login, the callback,
/// <c>/bff/user</c> and a local API behind the session cookie.
/// </summary>
public class BffEndToEndTests
{
    private const string UserName = "bff-e2e-user";
    private const string Password = "bff-e2e-password";

    private static IReadOnlyDictionary<string, object> NoSettings { get; } =
        new Dictionary<string, object>();

    #region Login Tests

    private static async Task SeedAsync(
        HostApplicationFactory factory,
        IReadOnlyDictionary<string, object>? settings = null
    )
    {
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedSharedSecretClientWithSettingsAsync(
            BffTestHost.ClientId,
            BffTestHost.ClientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            settings ?? NoSettings,
            BffTestHost.CallbackUri
        );
    }

    [Fact]
    public async Task Login_WithUserInfoLoad_CreatesTheSessionWithTheUserInfoClaims()
    {
        using var factory = new HostApplicationFactory();
        await SeedAsync(factory);
        var (keySet, valueSet, indexSet) = BffTestHost.CreateSharedState();
        var exchanges = new ConcurrentQueue<RecordedHttpExchange>();
        await using var host = await BffTestHost.StartAsync(
            factory,
            keySet,
            valueSet,
            indexSet,
            extraConfiguration: new Dictionary<string, string?>
            {
                [$"{BffTestHost.ConfigurationPrefix}:UserInfo:Load"] = "true",
            },
            exchanges: exchanges
        );

        // The callback requests UserInfo with the new access token; a failure would redirect with an error instead.
        using var callback = await CompleteLoginAsync(factory, host);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/", callback.Headers.Location?.OriginalString);

        // The test host configures no DPoP key, so UserInfo is requested with the bearer token the OP issued.
        var userInfo = exchanges.Last(exchange =>
            exchange.Path.EndsWith("/userinfo", StringComparison.Ordinal)
        );
        Assert.Equal(HttpStatusCode.OK, userInfo.StatusCode);
        Assert.Equal(OpenIdProtocol.TokenTypes.Bearer, userInfo.AuthorizationScheme);
        Assert.Null(userInfo.DPoPProof);

        var csrf = (BffDefaults.AntiforgeryHeaderName, BffDefaults.AntiforgeryHeaderValue);
        using var user = await host.SendAsync(
            HttpMethod.Get,
            $"{BffDefaults.EndpointPrefix}/user",
            csrf
        );
        Assert.Equal(HttpStatusCode.OK, user.StatusCode);
    }

    [Fact]
    public async Task Login_WithSignedUserInfo_ValidatesTheJwtAndCreatesTheSession()
    {
        using var factory = new HostApplicationFactory();
        await SeedAsync(
            factory,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.UserInfoSignedResponseAlg] = "RS256",
            }
        );
        var (keySet, valueSet, indexSet) = BffTestHost.CreateSharedState();
        var exchanges = new ConcurrentQueue<RecordedHttpExchange>();
        await using var host = await BffTestHost.StartAsync(
            factory,
            keySet,
            valueSet,
            indexSet,
            extraConfiguration: new Dictionary<string, string?>
            {
                [$"{BffTestHost.ConfigurationPrefix}:UserInfo:Load"] = "true",
                [$"{BffTestHost.ConfigurationPrefix}:UserInfo:SigningAlgorithms:0"] = "RS256",
            },
            exchanges: exchanges
        );

        // A UserInfo response that failed its validation would redirect with userinfo_failed instead.
        using var callback = await CompleteLoginAsync(factory, host);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/", callback.Headers.Location?.OriginalString);

        var userInfo = exchanges.Last(exchange =>
            exchange.Path.EndsWith("/userinfo", StringComparison.Ordinal)
        );
        Assert.Equal(HttpStatusCode.OK, userInfo.StatusCode);
        Assert.Equal(3, userInfo.ResponseBody?.Split('.').Length);
    }

    [Fact]
    public async Task Login_WithKeysAKmsHolds_SignsDecryptsAndProvesInTheKms()
    {
        const string prefix = BffTestHost.ConfigurationPrefix;
        const string clientRing = "bff-kms-client";
        const string dpopRing = "bff-kms-dpop";

        using var kms = new TestRemoteKeyProvider();
        var clientSignKmsKeyId = kms.AddKey(ECDsa.Create(ECCurve.NamedCurves.nistP256));
        var clientEncKmsKeyId = kms.AddKey(RSA.Create(3072));
        var dpopKmsKeyId = kms.AddKey(ECDsa.Create(ECCurve.NamedCurves.nistP256));

        string? jwks = null;
        using var factory = new HostApplicationFactory
        {
            ClientJwksResponder = _ =>
                jwks ?? throw new InvalidOperationException("The BFF's key set is not ready."),
        };
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedPrivateKeyJwtClientWithSettingsAsync(
            BffTestHost.ClientId,
            "https://bff.example/bff/jwks",
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.IdTokenEncryptedResponseAlg] = "RSA-OAEP-256",
            },
            BffTestHost.CallbackUri
        );

        var (keySet, valueSet, indexSet) = BffTestHost.CreateSharedState();
        var exchanges = new ConcurrentQueue<RecordedHttpExchange>();
        await using var host = await BffTestHost.StartAsync(
            factory,
            keySet,
            valueSet,
            indexSet,
            extraConfiguration: new Dictionary<string, string?>
            {
                [$"{prefix}:ClientAuthentication:Method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .PrivateKeyJwt,
                [$"{prefix}:ClientAuthentication:KeyRing"] = clientRing,
                [$"{prefix}:DPoP:KeyRing"] = dpopRing,
                [$"{prefix}:Jwks:Enabled"] = "true",
                [$"{prefix}:IdToken:KeyManagementAlgorithms:0"] = "RSA-OAEP-256",
                [$"Bff:KeyRings:{clientRing}:RemoteKeys:client-sig:Provider"] =
                    TestRemoteKeyProvider.ProviderName,
                [$"Bff:KeyRings:{clientRing}:RemoteKeys:client-sig:KeyId"] = clientSignKmsKeyId,
                [$"Bff:KeyRings:{clientRing}:RemoteKeys:client-sig:Algorithm"] = "ES256",
                [$"Bff:KeyRings:{clientRing}:RemoteKeys:client-sig:SecretType"] = "ecc",
                [$"Bff:KeyRings:{clientRing}:RemoteKeys:client-sig:KeySizeBits"] = "256",
                [$"Bff:KeyRings:{clientRing}:RemoteKeys:client-sig:Curve"] = "P-256",
                [$"Bff:KeyRings:{clientRing}:RemoteKeys:client-enc:Provider"] =
                    TestRemoteKeyProvider.ProviderName,
                [$"Bff:KeyRings:{clientRing}:RemoteKeys:client-enc:KeyId"] = clientEncKmsKeyId,
                [$"Bff:KeyRings:{clientRing}:RemoteKeys:client-enc:Algorithm"] = "RSA-OAEP-256",
                [$"Bff:KeyRings:{clientRing}:RemoteKeys:client-enc:SecretType"] = "rsa",
                [$"Bff:KeyRings:{clientRing}:RemoteKeys:client-enc:KeySizeBits"] = "3072",
                [$"Bff:KeyRings:{dpopRing}:RemoteKeys:dpop-sig:Provider"] =
                    TestRemoteKeyProvider.ProviderName,
                [$"Bff:KeyRings:{dpopRing}:RemoteKeys:dpop-sig:KeyId"] = dpopKmsKeyId,
                [$"Bff:KeyRings:{dpopRing}:RemoteKeys:dpop-sig:Algorithm"] = "ES256",
                [$"Bff:KeyRings:{dpopRing}:RemoteKeys:dpop-sig:SecretType"] = "ecc",
                [$"Bff:KeyRings:{dpopRing}:RemoteKeys:dpop-sig:KeySizeBits"] = "256",
                [$"Bff:KeyRings:{dpopRing}:RemoteKeys:dpop-sig:Curve"] = "P-256",
            },
            exchanges: exchanges,
            configureServices: serviceCollection =>
            {
                serviceCollection.AddSingleton<IRemoteKeyProvider>(kms);
                serviceCollection.AddSingleton<IRemoteKeyDescriptorSource>(kms);
            }
        );

        // Each remote key is proved when the host starts: a signature for each signing key, a decryption for the other.
        Assert.Equal(2, kms.SignatureCount);
        Assert.Equal(1, kms.DecryptionCount);
        var signaturesAfterStart = kms.SignatureCount;
        var decryptionsAfterStart = kms.DecryptionCount;

        using var jwksResponse = await host.SendAsync(
            HttpMethod.Get,
            $"{BffDefaults.EndpointPrefix}/jwks"
        );
        Assert.Equal(HttpStatusCode.OK, jwksResponse.StatusCode);
        var jwksBody = await jwksResponse.Content.ReadAsStringAsync();
        Assert.Contains("client-sig", jwksBody, StringComparison.Ordinal);
        Assert.Contains("client-enc", jwksBody, StringComparison.Ordinal);
        Assert.DoesNotContain("dpop-sig", jwksBody, StringComparison.Ordinal);
        jwks = jwksBody;

        using var callback = await CompleteLoginAsync(factory, host);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/", callback.Headers.Location?.OriginalString);

        var token = exchanges.Last(exchange =>
            exchange.Path.EndsWith("/token", StringComparison.Ordinal)
        );
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        Assert.Contains("client_assertion=", token.Body, StringComparison.Ordinal);
        Assert.NotNull(token.DPoPProof);

        // The KMS signs once for each client assertion and each DPoP proof the BFF sent,
        // and decrypts the ID token.
        var expectedSignatures =
            exchanges.Count(exchange =>
                exchange.Body?.Contains("client_assertion=", StringComparison.Ordinal) == true
            ) + exchanges.Count(exchange => exchange.DPoPProof is not null);
        Assert.Equal(expectedSignatures, kms.SignatureCount - signaturesAfterStart);
        Assert.Equal(1, kms.DecryptionCount - decryptionsAfterStart);
    }

    private static async Task<HttpResponseMessage> CompleteLoginAsync(
        HostApplicationFactory factory,
        BffTestHost host
    )
    {
        var browser = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
        var providerSession = await InteractiveLogin.SignInAsync(browser, UserName, Password);

        var loginPath =
            $"{BffDefaults.EndpointPrefix}/login?returnUrl=%2F&{OpenIdProtocol.Parameters.Prompt}={OpenIdProtocol.PromptTypes.None}";
        using var login = await host.SendAsync(HttpMethod.Get, loginPath);
        var authorizeUri = login.Headers.Location;
        Assert.NotNull(authorizeUri);

        using var authorizeRequest = new HttpRequestMessage(HttpMethod.Get, authorizeUri);
        authorizeRequest.Headers.Add(
            "Cookie",
            $"{InteractiveLogin.SessionCookieName}={providerSession}"
        );
        using var authorize = await browser.SendAsync(authorizeRequest);
        var callbackUri = authorize.Headers.Location;
        Assert.NotNull(callbackUri);

        return await host.SendAsync(HttpMethod.Get, callbackUri.PathAndQuery);
    }

    [Fact]
    public async Task Login_ThenUserAndApi_Succeed()
    {
        using var factory = new HostApplicationFactory();
        await SeedAsync(factory);
        var (keySet, valueSet, indexSet) = BffTestHost.CreateSharedState();
        await using var host = await BffTestHost.StartAsync(factory, keySet, valueSet, indexSet);

        var browser = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
        var providerSession = await InteractiveLogin.SignInAsync(browser, UserName, Password);

        var loginPath =
            $"{BffDefaults.EndpointPrefix}/login?returnUrl=%2F&{OpenIdProtocol.Parameters.Prompt}={OpenIdProtocol.PromptTypes.None}";
        using var login = await host.SendAsync(HttpMethod.Get, loginPath);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var authorizeUri = login.Headers.Location;
        Assert.NotNull(authorizeUri);

        using var authorizeRequest = new HttpRequestMessage(HttpMethod.Get, authorizeUri);
        authorizeRequest.Headers.Add(
            "Cookie",
            $"{InteractiveLogin.SessionCookieName}={providerSession}"
        );
        using var authorize = await browser.SendAsync(authorizeRequest);
        var callbackUri = authorize.Headers.Location;
        Assert.NotNull(callbackUri);
        Assert.StartsWith(
            BffTestHost.CallbackUri,
            callbackUri.OriginalString,
            StringComparison.Ordinal
        );

        using var callback = await host.SendAsync(HttpMethod.Get, callbackUri.PathAndQuery);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/", callback.Headers.Location?.OriginalString);

        var csrf = (BffDefaults.AntiforgeryHeaderName, BffDefaults.AntiforgeryHeaderValue);
        var userPath = $"{BffDefaults.EndpointPrefix}/user";
        using var user = await host.SendAsync(HttpMethod.Get, userPath, csrf);
        Assert.Equal(HttpStatusCode.OK, user.StatusCode);
        var userBody = await user.Content.ReadAsStringAsync();
        Assert.Contains("\"sub\"", userBody, StringComparison.Ordinal);

        using var userWithoutHeader = await host.SendAsync(HttpMethod.Get, userPath);
        Assert.Equal(HttpStatusCode.Forbidden, userWithoutHeader.StatusCode);

        using var api = await host.SendAsync(HttpMethod.Get, BffTestHost.ApiTokenPath, csrf);
        Assert.Equal(HttpStatusCode.OK, api.StatusCode);

        host.ClearCookies();
        using var anonymousApi = await host.SendAsync(
            HttpMethod.Get,
            BffTestHost.ApiTokenPath,
            csrf
        );
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousApi.StatusCode);
    }

    [Fact]
    public async Task Login_WithAForgedHost_RedirectsToTheConfiguredCallback()
    {
        using var factory = new HostApplicationFactory();
        await SeedAsync(factory);
        var (keySet, valueSet, indexSet) = BffTestHost.CreateSharedState();
        await using var host = await BffTestHost.StartAsync(factory, keySet, valueSet, indexSet);

        var browser = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
        var providerSession = await InteractiveLogin.SignInAsync(browser, UserName, Password);

        // The request names another host; the redirect_uri still comes from the frontend's PublicOrigin, so the provider accepts it
        // and sends the browser back to the registered callback.
        var loginUri =
            $"https://attacker.example{BffDefaults.EndpointPrefix}/login?returnUrl=%2F&{OpenIdProtocol.Parameters.Prompt}={OpenIdProtocol.PromptTypes.None}";
        using var login = await host.SendAsync(HttpMethod.Get, loginUri);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var authorizeUri = login.Headers.Location;
        Assert.NotNull(authorizeUri);
        Assert.DoesNotContain(
            "attacker.example",
            authorizeUri.OriginalString,
            StringComparison.Ordinal
        );

        using var authorizeRequest = new HttpRequestMessage(HttpMethod.Get, authorizeUri);
        authorizeRequest.Headers.Add(
            "Cookie",
            $"{InteractiveLogin.SessionCookieName}={providerSession}"
        );
        using var authorize = await browser.SendAsync(authorizeRequest);
        var callbackUri = authorize.Headers.Location;
        Assert.NotNull(callbackUri);
        Assert.StartsWith(
            BffTestHost.CallbackUri,
            callbackUri.OriginalString,
            StringComparison.Ordinal
        );

        using var callback = await host.SendAsync(HttpMethod.Get, callbackUri.PathAndQuery);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/", callback.Headers.Location?.OriginalString);
    }

    #endregion
}
