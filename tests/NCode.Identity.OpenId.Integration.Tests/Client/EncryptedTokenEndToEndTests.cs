// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Encoders;
using NCode.Identity.Jose;
using NCode.Identity.Jwt;
using NCode.Identity.OpenId.Client;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Client;

/// <summary>
/// Proves <see cref="IIdTokenValidator"/> against the NCode OpenID provider hosted in-process when the client asked
/// for encrypted ID tokens: the provider encrypts to the key the client publishes at its <c>jwks_uri</c> and the
/// client decrypts and validates the token.
/// </summary>
public class EncryptedTokenEndToEndTests
{
    private const string KeyRing = "encrypted-e2e";
    private const string ClientSecret = "encrypted-e2e-Kp4wT8nZ3xV7bQ2mL6cR9yH5jD1sGf0A";
    private const string RedirectUri = "https://client.example/callback";
    private const string BackChannelLogoutUri = "https://client.example/backchannel-logout";
    private const string EndSessionPath = "/oauth2/end_session";
    private const string BackChannelLogoutPath = "/backchannel-logout";
    private const string JwksUri = "https://client.example/jwks.json";
    private const string EncryptionKeyId = "client-enc";
    private const string UserName = "encrypted-e2e-user";
    private const string Password = "encrypted-e2e-password";
    private const string State = "encrypted-e2e-state";
    private const string Nonce = "encrypted-e2e-nonce";

    private static IReadOnlyDictionary<string, object> NoSettings { get; } =
        new Dictionary<string, object>();

    #region Scaffolding

    private static string BuildEncryptionJwks(RSA rsa)
    {
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        var jwk = new Dictionary<string, object>
        {
            ["kty"] = "RSA",
            ["use"] = "enc",
            ["alg"] = "RSA-OAEP-256",
            ["kid"] = EncryptionKeyId,
            ["n"] = Base64Url.Encode(parameters.Modulus!),
            ["e"] = Base64Url.Encode(parameters.Exponent!),
        };
        var jwks = new Dictionary<string, object> { ["keys"] = new[] { jwk } };
        return JsonSerializer.Serialize(jwks);
    }

    private static ServiceProvider CreateClientProvider(
        HostApplicationFactory factory,
        RSA clientRsa
    )
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection
            .AddSecretsLibrary()
            .AddJoseLibrary()
            .AddJwtLibrary()
            .AddOpenIdClientLibrary();
        // AddOpenIdClientLibrary registers no IExpiringKeySet for the logout-token validator.
        serviceCollection.AddSharedStateServices().AddInProcessExpiringKeySet();
        serviceCollection
            .AddHttpClient(OpenIdClientDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => factory.Server.CreateHandler());
        serviceCollection.AddKeyedSingleton<ISecretKeyCollectionProvider>(
            KeyRing,
            (serviceProvider, _) =>
            {
                var keyFactory = serviceProvider.GetRequiredService<ISecretKeyFactory>();
                return serviceProvider
                    .GetRequiredService<ISecretKeyCollectionProviderFactory>()
                    .CreateStatic([
                        keyFactory.CreateSymmetric(
                            new KeyMetadata { KeyId = "s" },
                            ClientSecret.AsSpan()
                        ),
                        keyFactory.CreateRsa(
                            new KeyMetadata
                            {
                                KeyId = EncryptionKeyId,
                                Use = SecretKeyUses.Encryption,
                            },
                            clientRsa
                        ),
                    ]);
            }
        );
        return serviceCollection.BuildServiceProvider();
    }

    private static ServiceProvider CreateRemoteClientProvider(
        HostApplicationFactory factory,
        TestRemoteKeyProvider kms,
        Func<SecretKey> decryptionKey
    )
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection
            .AddSecretsLibrary()
            .AddJoseLibrary()
            .AddJwtLibrary()
            .AddOpenIdClientLibrary();
        // AddOpenIdClientLibrary registers no IExpiringKeySet for the logout-token validator.
        serviceCollection.AddSharedStateServices().AddInProcessExpiringKeySet();
        serviceCollection
            .AddHttpClient(OpenIdClientDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => factory.Server.CreateHandler());
        serviceCollection.AddSingleton<IRemoteKeyProvider>(kms);
        serviceCollection.AddSingleton<IRemoteKeyDescriptorSource>(kms);
        serviceCollection.AddKeyedSingleton<ISecretKeyCollectionProvider>(
            KeyRing,
            (serviceProvider, _) =>
            {
                var keyFactory = serviceProvider.GetRequiredService<ISecretKeyFactory>();
                return serviceProvider
                    .GetRequiredService<ISecretKeyCollectionProviderFactory>()
                    .CreateStatic([
                        keyFactory.CreateSymmetric(
                            new KeyMetadata { KeyId = "s" },
                            ClientSecret.AsSpan()
                        ),
                        decryptionKey(),
                    ]);
            }
        );
        return serviceCollection.BuildServiceProvider();
    }

    private static ClientRegistration CreateRegistration(string clientId) =>
        new(
            HostApplicationFactory.TestServerIssuer,
            clientId,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        )
        {
            KeyRing = KeyRing,
            IdTokenKeyManagementAlgorithms = ["RSA-OAEP-256"],
        };

    private static ValueTask SeedClientAsync(
        HostApplicationFactory factory,
        string clientId,
        IReadOnlyDictionary<string, object> extraSettings,
        CancellationToken cancellationToken
    )
    {
        var settings = new Dictionary<string, object>
        {
            [OpenIdSettingNames.BackChannelLogoutUri] = BackChannelLogoutUri,
            [OpenIdSettingNames.JwksUri] = JwksUri,
            [OpenIdSettingNames.IdTokenEncryptedResponseAlg] = "RSA-OAEP-256",
        };
        foreach (var (name, value) in extraSettings)
            settings[name] = value;

        return factory.SeedSharedSecretClientWithSettingsAsync(
            clientId,
            ClientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            settings,
            [RedirectUri],
            cancellationToken
        );
    }

    private static HttpClient CreateBrowser(HostApplicationFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    // Follows the authorization URL as the signed-in end-user's browser (prompt=none lets the session answer without an
    // interactive page), redeems the code with the client and returns the raw ID token with its verified payload.
    private static async ValueTask<(
        string IdToken,
        JsonElement Payload,
        string AccessToken
    )> RequestIdTokenAsync(
        HttpClient browser,
        string sessionCookie,
        ServiceProvider provider,
        ClientRegistration registration,
        CancellationToken cancellationToken
    )
    {
        var request = new AuthorizationRequest(RedirectUri, State, TestPkce.CodeVerifier)
        {
            Nonce = Nonce,
            Scope = OpenIdProtocol.ScopeTypes.OpenId,
            AdditionalParameters = new Dictionary<string, string>
            {
                [OpenIdProtocol.Parameters.Prompt] = OpenIdProtocol.PromptTypes.None,
            },
        };
        var authorizationResult = await provider
            .GetRequiredService<IAuthorizationRequestClient>()
            .CreateAuthorizationUriAsync(registration, request, cancellationToken);
        Assert.True(authorizationResult.IsSuccess, authorizationResult.Error?.ToString());

        using var authorizeRequest = new HttpRequestMessage(
            HttpMethod.Get,
            authorizationResult.AuthorizationUri
        );
        authorizeRequest.Headers.Add(
            "Cookie",
            $"{InteractiveLogin.SessionCookieName}={sessionCookie}"
        );
        using var authorizeResponse = await browser.SendAsync(authorizeRequest, cancellationToken);

        var location = authorizeResponse.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith(RedirectUri, location.OriginalString, StringComparison.Ordinal);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.Equal(State, parameters[OpenIdProtocol.Parameters.State]);
        var code = parameters[OpenIdProtocol.Parameters.AuthorizationCode];
        Assert.True(
            !string.IsNullOrEmpty(code),
            $"error='{parameters[OpenIdProtocol.Parameters.ErrorCode]}'"
        );

        var tokenResult = await provider
            .GetRequiredService<ITokenClient>()
            .RequestTokenAsync(
                registration,
                new AuthorizationCodeTokenRequest(code, RedirectUri, TestPkce.CodeVerifier),
                cancellationToken
            );
        Assert.True(tokenResult.IsSuccess, tokenResult.Error?.ToString());
        var idToken = tokenResult.Response.IdToken;
        Assert.False(string.IsNullOrEmpty(idToken));

        var validation = await provider
            .GetRequiredService<IIdTokenValidator>()
            .ValidateAsync(
                registration,
                new IdTokenValidationRequest
                {
                    IdToken = idToken,
                    Nonce = Nonce,
                    AccessToken = tokenResult.Response.AccessToken,
                    AuthorizationCode = code,
                },
                cancellationToken
            );
        Assert.True(validation.IsValid, validation.FailureReason);
        var accessToken = tokenResult.Response.AccessToken;
        Assert.NotNull(accessToken);
        return (idToken, validation.Payload, accessToken);
    }

    private static JsonElement ReadEncryptionHeader(string token)
    {
        var segments = token.Split('.');
        Assert.Equal(5, segments.Length);
        var header = JsonElement.Parse(System.Buffers.Text.Base64Url.DecodeFromChars(segments[0]));
        return header;
    }

    private static async ValueTask EndSessionAsync(
        HttpClient browser,
        string sessionCookie,
        CancellationToken cancellationToken
    )
    {
        using var response = await InteractiveLogin.ConfirmEndSessionAsync(
            browser,
            EndSessionPath,
            sessionCookie,
            cancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The provider delivers the logout token on a background worker, so it arrives after the end-session response.
    private static async ValueTask<string> WaitForLogoutTokenAsync(
        ConcurrentQueue<RecordedHttpExchange> exchanges,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        while (exchanges.IsEmpty && stopwatch.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        var delivery = Assert.Single(exchanges);
        Assert.Equal(BackChannelLogoutPath, delivery.Path);

        var logoutToken = HttpUtility.ParseQueryString(delivery.Body ?? string.Empty)[
            OpenIdProtocol.Parameters.LogoutToken
        ];
        Assert.False(string.IsNullOrEmpty(logoutToken));
        return logoutToken;
    }

    private static async ValueTask<LogoutTokenValidationResult> ValidateLogoutTokenAsync(
        ServiceProvider provider,
        ClientRegistration registration,
        string logoutToken,
        CancellationToken cancellationToken
    ) =>
        await provider
            .GetRequiredService<ILogoutTokenValidator>()
            .ValidateAsync(
                registration,
                new LogoutTokenValidationRequest { LogoutToken = logoutToken },
                cancellationToken
            );

    #endregion

    #region GetUserInfoAsync Tests

    [Fact]
    public async Task GetUserInfoAsync_WithAResponseTheProviderSignedAndEncryptedToTheClient_ReturnsItsClaims()
    {
        const string clientId = "it-encrypted-e2e-userinfo";

        using var clientRsa = RSA.Create(2048);
        var jwks = BuildEncryptionJwks(clientRsa);
        using var factory = new HostApplicationFactory
        {
            BackChannelLogoutExchanges = new(),
            ClientJwksResponder = _ => jwks,
        };
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await SeedClientAsync(
            factory,
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.UserInfoSignedResponseAlg] = "RS256",
                [OpenIdSettingNames.UserInfoEncryptedResponseAlg] = "RSA-OAEP-256",
                [OpenIdSettingNames.UserInfoEncryptedResponseEnc] = "A256GCM",
            },
            CancellationToken.None
        );
        await using var provider = CreateClientProvider(factory, clientRsa);
        var registration = new ClientRegistration(
            HostApplicationFactory.TestServerIssuer,
            clientId,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        )
        {
            KeyRing = KeyRing,
            IdTokenKeyManagementAlgorithms = ["RSA-OAEP-256"],
            UserInfoSigningAlgorithms = ["RS256"],
            UserInfoKeyManagementAlgorithms = ["RSA-OAEP-256"],
            UserInfoContentEncryptionAlgorithms = ["A256GCM"],
        };
        var browser = CreateBrowser(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            browser,
            UserName,
            Password,
            CancellationToken.None
        );
        var (_, idTokenPayload, accessToken) = await RequestIdTokenAsync(
            browser,
            sessionCookie,
            provider,
            registration,
            CancellationToken.None
        );
        var subject = idTokenPayload.GetProperty("sub").GetString()!;

        var result = await provider
            .GetRequiredService<IUserInfoClient>()
            .GetUserInfoAsync(
                registration,
                new UserInfoRequest(accessToken, subject),
                CancellationToken.None
            );

        // The Nested JWT decrypted with the client's key and verified with the provider's: OIDC Core 1.0 §5.3.2.
        Assert.True(result.IsSuccess, result.FailureReason ?? result.Error?.ToString());
        Assert.Equal(subject, result.Claims.GetProperty("sub").GetString());
        Assert.Equal(clientId, result.Claims.GetProperty("aud").GetString());
        Assert.Equal(
            HostApplicationFactory.TestServerIssuer,
            result.Claims.GetProperty("iss").GetString()
        );
    }

    #endregion

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_WithAnIdTokenTheProviderEncryptedToTheClient_ReturnsItsPayload()
    {
        const string clientId = "it-encrypted-e2e-id-token";

        using var clientRsa = RSA.Create(2048);
        var jwks = BuildEncryptionJwks(clientRsa);
        using var factory = new HostApplicationFactory
        {
            BackChannelLogoutExchanges = new(),
            ClientJwksResponder = _ => jwks,
        };
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await SeedClientAsync(factory, clientId, NoSettings, CancellationToken.None);
        await using var provider = CreateClientProvider(factory, clientRsa);
        var registration = CreateRegistration(clientId);
        var browser = CreateBrowser(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            browser,
            UserName,
            Password,
            CancellationToken.None
        );

        var (idToken, payload, _) = await RequestIdTokenAsync(
            browser,
            sessionCookie,
            provider,
            registration,
            CancellationToken.None
        );

        var header = ReadEncryptionHeader(idToken);
        Assert.Equal("RSA-OAEP-256", header.GetProperty(JoseClaimNames.Header.Alg).GetString());
        Assert.Equal("A128CBC-HS256", header.GetProperty(JoseClaimNames.Header.Enc).GetString());
        Assert.Equal(EncryptionKeyId, header.GetProperty("kid").GetString());
        Assert.Equal("JWT", header.GetProperty("cty").GetString());
        Assert.False(header.TryGetProperty(JoseClaimNames.Header.Zip, out _));
        Assert.Equal(Nonce, payload.GetProperty("nonce").GetString());
        Assert.False(string.IsNullOrEmpty(payload.GetProperty("sub").GetString()));
    }

    [Fact]
    public async Task ValidateAsync_WithAnIdTokenEncryptedToAKeyAKmsHolds_DecryptsItInTheKms()
    {
        const string clientId = "it-encrypted-e2e-kms-id-token";

        using var kms = new TestRemoteKeyProvider();
        var clientRsa = RSA.Create(3072);
        var jwks = BuildEncryptionJwks(clientRsa);
        var kmsKeyId = kms.AddKey(clientRsa);

        using var factory = new HostApplicationFactory
        {
            BackChannelLogoutExchanges = new(),
            ClientJwksResponder = _ => jwks,
        };
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await SeedClientAsync(factory, clientId, NoSettings, CancellationToken.None);

        SecretKey? remoteKey = null;
        await using var provider = CreateRemoteClientProvider(
            factory,
            kms,
            () =>
                remoteKey ?? throw new InvalidOperationException("The decryption key is not read.")
        );
        var resolver = provider.GetRequiredService<IRemoteKeyResolver>();
        var resolution = resolver.Resolve(
            new RemoteKeyAlgorithmSelector
            {
                Provider = TestRemoteKeyProvider.ProviderName,
                Algorithm = "RSA-OAEP-256",
                SecretType = SecretKeyTypes.Rsa,
                KeySizeBits = 3072,
            }
        );
        remoteKey = await resolver.ReadKeyAsync(
            resolution.Bind(kmsKeyId),
            new KeyMetadata { KeyId = EncryptionKeyId },
            CancellationToken.None
        );
        await provider
            .GetRequiredService<IRemoteKeyProver>()
            .ProveAsync(remoteKey, CancellationToken.None);
        var decryptionsBefore = kms.DecryptionCount;

        var registration = CreateRegistration(clientId);
        var browser = CreateBrowser(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            browser,
            UserName,
            Password,
            CancellationToken.None
        );

        var (idToken, payload, _) = await RequestIdTokenAsync(
            browser,
            sessionCookie,
            provider,
            registration,
            CancellationToken.None
        );

        var header = ReadEncryptionHeader(idToken);
        Assert.Equal("RSA-OAEP-256", header.GetProperty(JoseClaimNames.Header.Alg).GetString());
        Assert.Equal(EncryptionKeyId, header.GetProperty("kid").GetString());
        Assert.Equal(Nonce, payload.GetProperty("nonce").GetString());
        Assert.IsAssignableFrom<IRemoteSecretKey>(remoteKey);
        Assert.Equal(SecretKeyUses.Encryption, remoteKey.Metadata.Use);
        Assert.Equal(1, kms.DecryptionCount - decryptionsBefore);
    }

    [Fact]
    public async Task ValidateAsync_WithALogoutTokenTheProviderEncryptedToTheClient_ReturnsItsSubjectAndSession()
    {
        const string clientId = "it-encrypted-e2e-logout";

        using var clientRsa = RSA.Create(2048);
        var jwks = BuildEncryptionJwks(clientRsa);
        var exchanges = new ConcurrentQueue<RecordedHttpExchange>();
        using var factory = new HostApplicationFactory
        {
            BackChannelLogoutExchanges = exchanges,
            ClientJwksResponder = _ => jwks,
        };
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await SeedClientAsync(factory, clientId, NoSettings, CancellationToken.None);
        await using var provider = CreateClientProvider(factory, clientRsa);
        var registration = CreateRegistration(clientId);
        var browser = CreateBrowser(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            browser,
            UserName,
            Password,
            CancellationToken.None
        );
        var (_, idTokenPayload, _) = await RequestIdTokenAsync(
            browser,
            sessionCookie,
            provider,
            registration,
            CancellationToken.None
        );

        await EndSessionAsync(browser, sessionCookie, CancellationToken.None);
        var logoutToken = await WaitForLogoutTokenAsync(exchanges, CancellationToken.None);

        var header = ReadEncryptionHeader(logoutToken);
        Assert.Equal("RSA-OAEP-256", header.GetProperty(JoseClaimNames.Header.Alg).GetString());
        Assert.Equal("A128CBC-HS256", header.GetProperty(JoseClaimNames.Header.Enc).GetString());
        Assert.Equal(EncryptionKeyId, header.GetProperty("kid").GetString());
        Assert.Equal("JWT", header.GetProperty("cty").GetString());
        Assert.Equal(
            HostApplicationFactory.TestServerIssuer,
            header.GetProperty("iss").GetString()
        );

        var result = await ValidateLogoutTokenAsync(
            provider,
            registration,
            logoutToken,
            CancellationToken.None
        );

        Assert.True(result.IsValid, result.FailureReason);
        Assert.Equal(HostApplicationFactory.TestServerIssuer, result.Issuer);
        Assert.False(string.IsNullOrEmpty(result.TokenId));
        Assert.Equal(idTokenPayload.GetProperty("sub").GetString(), result.Subject);
        Assert.Equal(idTokenPayload.GetProperty("sid").GetString(), result.SessionId);
    }

    #endregion
}
