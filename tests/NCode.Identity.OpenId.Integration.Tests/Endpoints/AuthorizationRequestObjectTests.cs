// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Exercises request objects (RFC 9101) at the authorization endpoint: the algorithms a request object may use are those
/// the requesting client's effective settings permit, which a client narrows by registering a
/// <c>request_object_signing_alg</c>.
/// </summary>
// See ADR-1155.
public sealed class AuthorizationRequestObjectTests : IDisposable
{
    private const string UserName = "jar-user";
    private const string Password = "jar-password";
    private const string RedirectUri = "https://client.example/callback";
    private const string State = "jar-state";

    private RSA ClientRsa { get; } = RSA.Create(2048);

    public void Dispose() => ClientRsa.Dispose();

    #region Scaffolding

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    private async ValueTask<string> RegisterClientAsync(
        HttpClient client,
        CancellationToken cancellationToken
    ) => (await RegisterClientWithSecretAsync(client, cancellationToken)).ClientId;

    private async ValueTask<(string ClientId, string ClientSecret)> RegisterClientWithSecretAsync(
        HttpClient client,
        CancellationToken cancellationToken
    )
    {
        var parameters = ClientRsa.ExportParameters(includePrivateParameters: false);
        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["redirect_uris"] = new[] { RedirectUri },
                ["token_endpoint_auth_method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .ClientSecretBasic,
                ["request_object_signing_alg"] = "RS256",
                ["jwks"] = new Dictionary<string, object>
                {
                    ["keys"] = new[]
                    {
                        new Dictionary<string, string>
                        {
                            ["kty"] = "RSA",
                            ["kid"] = "client-sig",
                            ["use"] = "sig",
                            ["n"] = Base64Url.EncodeToString(parameters.Modulus),
                            ["e"] = Base64Url.EncodeToString(parameters.Exponent),
                        },
                    },
                },
            },
            cancellationToken
        );

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);

        var registration = JsonElement.Parse(body);
        Assert.Equal("RS256", registration.GetProperty("request_object_signing_alg").GetString());
        return (
            registration.GetProperty("client_id").GetString() ?? string.Empty,
            registration.GetProperty("client_secret").GetString() ?? string.Empty
        );
    }

    private static async ValueTask<string> GetIssuerAsync(
        HttpClient client,
        CancellationToken cancellationToken
    )
    {
        using var response = await client.GetAsync(
            "/.well-known/openid-configuration",
            cancellationToken
        );
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonElement = JsonElement.Parse(body);
        return jsonElement.GetProperty("issuer").GetString()!;
    }

    private ValueTask<string> CreateRequestObjectAsync(
        HostApplicationFactory factory,
        string clientId,
        string algorithmCode,
        string? audience,
        RequestObjectOptions options,
        CancellationToken cancellationToken
    ) =>
        CreateRequestObjectAsync(
            factory,
            clientId,
            algorithmCode,
            audience,
            options,
            prompt: "none",
            cancellationToken
        );

    // A null prompt omits the claim, so a request without a session is sent to the login page.
    private async ValueTask<string> CreateRequestObjectAsync(
        HostApplicationFactory factory,
        string clientId,
        string algorithmCode,
        string? audience,
        RequestObjectOptions options,
        string? prompt,
        CancellationToken cancellationToken
    )
    {
        var now = DateTimeOffset.UtcNow;
        var algorithms = factory
            .Services.GetRequiredService<IAlgorithmCollectionProvider>()
            .Collection;
        Assert.True(algorithms.TryGetSignatureAlgorithm(algorithmCode, out var algorithm));

        var clientKey = factory
            .Services.GetRequiredService<ISecretKeyFactory>()
            .CreateRsa(new KeyMetadata { KeyId = "client-sig" }, ClientRsa);

        var claims = new Dictionary<string, object>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["scope"] = "openid",
            ["state"] = State,
            ["code_challenge"] = TestPkce.CodeChallenge,
            ["code_challenge_method"] = "S256",
        };
        if (prompt is not null)
            claims["prompt"] = prompt;
        if (options.ResponseMode is not null)
            claims["response_mode"] = options.ResponseMode;
        if (options.RedirectUri is not null)
            claims["redirect_uri"] = options.RedirectUri;

        var encoder = factory.Services.GetRequiredService<IJwtEncoder>();
        return await encoder.EncodeAsync(
            new JwtEncodingParameters
            {
                SigningCredentials = new JoseSigningCredentials(clientKey, algorithm),
                Issuer = clientId,
                Audience = audience,
                IssuedAt = now,
                NotBefore = now,
                Expires = now + (options.Lifetime ?? TimeSpan.FromMinutes(5)),
                ExtraPayloadClaims = claims,
            },
            cancellationToken
        );
    }

    private static async ValueTask<HttpResponseMessage> AuthorizeAsync(
        HttpClient client,
        string? sessionCookie,
        string clientId,
        string requestObject,
        string outerParameters,
        CancellationToken cancellationToken
    )
    {
        var query =
            $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope=openid"
            + $"&request={Uri.EscapeDataString(requestObject)}"
            + outerParameters;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth2/authorize" + query);
        if (sessionCookie is not null)
            request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        return await client.SendAsync(request, cancellationToken);
    }

    // RFC 9126 §3: the body carries only the request object; the client authenticates with client_secret_basic.
    private static async ValueTask<HttpResponseMessage> PushRequestObjectAsync(
        HttpClient client,
        string clientId,
        string clientSecret,
        string requestObject,
        IEnumerable<KeyValuePair<string, string>> outerParameters,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/par");
        var credentials = Uri.EscapeDataString(clientId) + ":" + Uri.EscapeDataString(clientSecret);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(SecureEncoding.UTF8.GetBytes(credentials))
        );
        request.Content = new FormUrlEncodedContent([
            new("request", requestObject),
            .. outerParameters,
        ]);
        return await client.SendAsync(request, cancellationToken);
    }

    private static async ValueTask<HttpResponseMessage> AuthorizeWithRequestUriAsync(
        HttpClient client,
        string sessionCookie,
        string clientId,
        string requestUri,
        CancellationToken cancellationToken
    )
    {
        var query =
            $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&request_uri={Uri.EscapeDataString(requestUri)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth2/authorize" + query);
        request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        return await client.SendAsync(request, cancellationToken);
    }

    // The authorization endpoint redirects an error to a validated redirect URI, or renders it when it cannot.
    private static async ValueTask<(string? Code, string? Error)> ReadResultAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        if (response.Headers.Location is { } location)
        {
            var parameters = HttpUtility.ParseQueryString(location.Query);
            return (parameters["code"], parameters["error"]);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return (null, body);
    }

    #endregion

    #region Authorize Tests

    [Fact]
    public async Task Authorize_WhenRequestObjectSignedWithTheRegisteredAlgorithm_IssuesCode()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        var client = CreateClient(factory);
        var clientId = await RegisterClientAsync(client, CancellationToken.None);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        var issuer = await GetIssuerAsync(client, CancellationToken.None);
        var requestObject = await CreateRequestObjectAsync(
            factory,
            clientId,
            "RS256",
            issuer,
            new RequestObjectOptions { RedirectUri = RedirectUri },
            CancellationToken.None
        );

        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            clientId,
            requestObject,
            outerParameters: "",
            CancellationToken.None
        );

        var (code, error) = await ReadResultAsync(response, CancellationToken.None);
        Assert.True(!string.IsNullOrEmpty(code), $"error='{error}'");
    }

    [Fact]
    public async Task Authorize_WhenRequestObjectRequestsTheJwtResponseMode_ReturnsTheResponseAsAJwt()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        var client = CreateClient(factory);
        var clientId = await RegisterClientAsync(client, CancellationToken.None);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        var issuer = await GetIssuerAsync(client, CancellationToken.None);
        var requestObject = await CreateRequestObjectAsync(
            factory,
            clientId,
            "RS256",
            issuer,
            new RequestObjectOptions
            {
                ResponseMode = OpenIdProtocol.ResponseModes.Jwt,
                RedirectUri = RedirectUri,
            },
            CancellationToken.None
        );

        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            clientId,
            requestObject,
            outerParameters: "",
            CancellationToken.None
        );

        // The 'jwt' shorthand resolves to 'query.jwt' for a code response (JARM §2.3.4).
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.Null(parameters["code"]);
        Assert.False(string.IsNullOrEmpty(parameters["response"]));
    }

    [Fact]
    public async Task Authorize_WhenAParameterIsSentOnlyOutsideTheRequestObject_IgnoresIt()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        var client = CreateClient(factory);
        var clientId = await RegisterClientAsync(client, CancellationToken.None);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        var issuer = await GetIssuerAsync(client, CancellationToken.None);
        var requestObject = await CreateRequestObjectAsync(
            factory,
            clientId,
            "RS256",
            issuer,
            new RequestObjectOptions { RedirectUri = RedirectUri },
            CancellationToken.None
        );

        // The request object omits response_mode, so the unsigned outer 'fragment' must not apply (RFC 9101 §6.3).
        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            clientId,
            requestObject,
            $"&response_mode={OpenIdProtocol.ResponseModes.Fragment}",
            CancellationToken.None
        );

        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.DoesNotContain("code=", location.Fragment, StringComparison.Ordinal);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.False(string.IsNullOrEmpty(parameters["code"]), location.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://client.example/other")]
    public async Task Authorize_WhenRequestObjectLacksTheOuterRedirectUri_ReturnsInvalidRequestObject(
        string? requestObjectRedirectUri
    )
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        var client = CreateClient(factory);
        var clientId = await RegisterClientAsync(client, CancellationToken.None);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        var issuer = await GetIssuerAsync(client, CancellationToken.None);
        var requestObject = await CreateRequestObjectAsync(
            factory,
            clientId,
            "RS256",
            issuer,
            new RequestObjectOptions { RedirectUri = requestObjectRedirectUri },
            CancellationToken.None
        );

        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            clientId,
            requestObject,
            outerParameters: "",
            CancellationToken.None
        );

        var (code, error) = await ReadResultAsync(response, CancellationToken.None);
        Assert.Null(code);
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequestJwt, error);
    }

    [Fact]
    public async Task Authorize_WhenVerifiedRequestObjectIsRefused_DeliversTheErrorWithItsStateAndResponseMode()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        var client = CreateClient(factory);
        var clientId = await RegisterClientAsync(client, CancellationToken.None);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        var issuer = await GetIssuerAsync(client, CancellationToken.None);
        // The object is verified but lacks its redirect_uri, so validation refuses it after verification.
        var requestObject = await CreateRequestObjectAsync(
            factory,
            clientId,
            "RS256",
            issuer,
            new RequestObjectOptions { ResponseMode = OpenIdProtocol.ResponseModes.Fragment },
            CancellationToken.None
        );

        // Only the object's state and response mode apply to the error (RFC 9101 §6.3, RFC 6749 §4.1.2.1).
        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            clientId,
            requestObject,
            "&state=outer-state",
            CancellationToken.None
        );

        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal(RedirectUri, location.GetLeftPart(UriPartial.Path));
        Assert.Null(HttpUtility.ParseQueryString(location.Query)["error"]);
        var parameters = HttpUtility.ParseQueryString(location.Fragment.TrimStart('#'));
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequestJwt, parameters["error"]);
        Assert.Equal(State, parameters["state"]);
    }

    [Fact]
    public async Task Authorize_WhenVerifiedRequestObjectRequestsJwtAndIsRefused_ReturnsTheErrorAsAJwtWithItsState()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        var client = CreateClient(factory);
        var clientId = await RegisterClientAsync(client, CancellationToken.None);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        var issuer = await GetIssuerAsync(client, CancellationToken.None);
        var requestObject = await CreateRequestObjectAsync(
            factory,
            clientId,
            "RS256",
            issuer,
            new RequestObjectOptions { ResponseMode = OpenIdProtocol.ResponseModes.Jwt },
            CancellationToken.None
        );

        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            clientId,
            requestObject,
            "&state=outer-state",
            CancellationToken.None
        );

        // The 'jwt' shorthand resolves to 'query.jwt' for a code response (JARM §2.3.4), so the error is signed too.
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.Null(parameters["error"]);
        Assert.Null(parameters["state"]);
        var jwt = parameters["response"];
        Assert.False(string.IsNullOrEmpty(jwt), location.ToString());
        var payload = JsonElement.Parse(Base64Url.DecodeFromChars(jwt.Split('.')[1]));
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRequestJwt,
            payload.GetProperty("error").GetString()
        );
        Assert.Equal(State, payload.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Authorize_WhenRequestObjectSignedWithAnotherAlgorithm_ReturnsInvalidRequestObject()
    {
        // The client registered RS256, so a request object signed with PS256 — even by its own key — is refused
        // before its signature is verified (OpenID Connect Dynamic Client Registration 1.0 §2).
        using var factory = new AnonymousRegistrationApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        var client = CreateClient(factory);
        var clientId = await RegisterClientAsync(client, CancellationToken.None);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        var issuer = await GetIssuerAsync(client, CancellationToken.None);
        var requestObject = await CreateRequestObjectAsync(
            factory,
            clientId,
            "PS256",
            issuer,
            new RequestObjectOptions { RedirectUri = RedirectUri },
            CancellationToken.None
        );

        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            clientId,
            requestObject,
            outerParameters: "",
            CancellationToken.None
        );

        var (code, error) = await ReadResultAsync(response, CancellationToken.None);
        Assert.True(string.IsNullOrEmpty(code));
        Assert.NotNull(error);
        Assert.Contains(
            OpenIdProtocol.ErrorCodes.InvalidRequestJwt,
            error,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Authorize_WhenRequestObjectIsNotAddressedToTheIssuer_ReturnsInvalidRequestObject()
    {
        // A request object is addressed to the authorization server, so one without the issuer in its 'aud' is
        // refused (RFC 9101 §4, ADR-1187).
        using var factory = new AnonymousRegistrationApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        var client = CreateClient(factory);
        var clientId = await RegisterClientAsync(client, CancellationToken.None);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        var requestObject = await CreateRequestObjectAsync(
            factory,
            clientId,
            "RS256",
            audience: null,
            new RequestObjectOptions { RedirectUri = RedirectUri },
            CancellationToken.None
        );

        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            clientId,
            requestObject,
            outerParameters: "",
            CancellationToken.None
        );

        var (code, error) = await ReadResultAsync(response, CancellationToken.None);
        Assert.True(string.IsNullOrEmpty(code));
        Assert.NotNull(error);
        Assert.Contains(
            OpenIdProtocol.ErrorCodes.InvalidRequestJwt,
            error,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Authorize_WhenRequestObjectOutlivesTheMaximumLifetime_ReturnsInvalidRequestObject()
    {
        // The default request_object_max_lifetime is 60 minutes, so a request object valid for longer is refused
        // (ADR-1187).
        using var factory = new AnonymousRegistrationApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        var client = CreateClient(factory);
        var clientId = await RegisterClientAsync(client, CancellationToken.None);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        var issuer = await GetIssuerAsync(client, CancellationToken.None);
        var requestObject = await CreateRequestObjectAsync(
            factory,
            clientId,
            "RS256",
            issuer,
            new RequestObjectOptions
            {
                Lifetime = TimeSpan.FromMinutes(61),
                RedirectUri = RedirectUri,
            },
            CancellationToken.None
        );

        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            clientId,
            requestObject,
            outerParameters: "",
            CancellationToken.None
        );

        var (code, error) = await ReadResultAsync(response, CancellationToken.None);
        Assert.True(string.IsNullOrEmpty(code));
        Assert.NotNull(error);
        Assert.Contains(
            OpenIdProtocol.ErrorCodes.InvalidRequestJwt,
            error,
            StringComparison.Ordinal
        );
    }

    #endregion

    #region Continue Tests

    [Fact]
    public async Task Continue_WhenTheRequestCarriesARequestObject_AfterTheUserSignsIn_IssuesTheCode()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        var client = CreateClient(factory);
        var clientId = await RegisterClientAsync(client, CancellationToken.None);
        var issuer = await GetIssuerAsync(client, CancellationToken.None);
        var requestObject = await CreateRequestObjectAsync(
            factory,
            clientId,
            "RS256",
            issuer,
            new RequestObjectOptions { RedirectUri = RedirectUri },
            prompt: null,
            CancellationToken.None
        );

        // Without a session the request is sent to the login page with the continue URL as its return URL.
        using var authorizeResponse = await AuthorizeAsync(
            client,
            sessionCookie: null,
            clientId,
            requestObject,
            outerParameters: "",
            CancellationToken.None
        );

        Assert.Equal(HttpStatusCode.Found, authorizeResponse.StatusCode);
        Assert.NotNull(client.BaseAddress);
        Assert.NotNull(authorizeResponse.Headers.Location);
        var loginLocation = new Uri(client.BaseAddress, authorizeResponse.Headers.Location);
        var continueUrl = HttpUtility.ParseQueryString(loginLocation.Query)["ReturnUrl"];
        Assert.False(string.IsNullOrEmpty(continueUrl));

        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );

        using var request = new HttpRequestMessage(HttpMethod.Get, continueUrl);
        request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        using var continueResponse = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.SeeOther, continueResponse.StatusCode);
        var location = continueResponse.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal(RedirectUri, location.GetLeftPart(UriPartial.Path));
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.False(string.IsNullOrEmpty(parameters["code"]), location.ToString());
        Assert.Equal(State, parameters["state"]);
    }

    #endregion

    #region Pushed Request Object Tests

    [Fact]
    public async Task Authorize_WhenPushedBodyIsOnlyTheRequestObject_IssuesCodeToTheObjectsRedirectUri()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        var client = CreateClient(factory);
        var (clientId, clientSecret) = await RegisterClientWithSecretAsync(
            client,
            CancellationToken.None
        );
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        var issuer = await GetIssuerAsync(client, CancellationToken.None);
        var requestObject = await CreateRequestObjectAsync(
            factory,
            clientId,
            "RS256",
            issuer,
            new RequestObjectOptions { RedirectUri = RedirectUri },
            CancellationToken.None
        );

        using var parResponse = await PushRequestObjectAsync(
            client,
            clientId,
            clientSecret,
            requestObject,
            outerParameters: [],
            CancellationToken.None
        );
        var parBody = await parResponse.Content.ReadAsStringAsync();
        Assert.True(parResponse.StatusCode == HttpStatusCode.Created, parBody);
        var requestUri = JsonElement.Parse(parBody).GetProperty("request_uri").GetString();
        Assert.NotNull(requestUri);

        using var response = await AuthorizeWithRequestUriAsync(
            client,
            sessionCookie,
            clientId,
            requestUri,
            CancellationToken.None
        );

        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal(RedirectUri, location.GetLeftPart(UriPartial.Path));
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.False(string.IsNullOrEmpty(parameters["code"]), location.ToString());
        Assert.Equal(State, parameters["state"]);
    }

    [Fact]
    public async Task PushedAuthorization_WhenRequestObjectRedirectUriIsNotRegistered_ReturnsUnauthorizedClient()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = CreateClient(factory);
        var (clientId, clientSecret) = await RegisterClientWithSecretAsync(
            client,
            CancellationToken.None
        );
        var issuer = await GetIssuerAsync(client, CancellationToken.None);
        var requestObject = await CreateRequestObjectAsync(
            factory,
            clientId,
            "RS256",
            issuer,
            new RequestObjectOptions { RedirectUri = "https://attacker.example/callback" },
            CancellationToken.None
        );

        using var parResponse = await PushRequestObjectAsync(
            client,
            clientId,
            clientSecret,
            requestObject,
            outerParameters: [],
            CancellationToken.None
        );

        var parBody = await parResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, parResponse.StatusCode);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.UnauthorizedClient,
            JsonElement.Parse(parBody).GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PushedAuthorization_WhenOuterRedirectUriDiffersFromTheRequestObject_ReturnsInvalidRequestObject()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = CreateClient(factory);
        var (clientId, clientSecret) = await RegisterClientWithSecretAsync(
            client,
            CancellationToken.None
        );
        var issuer = await GetIssuerAsync(client, CancellationToken.None);
        var requestObject = await CreateRequestObjectAsync(
            factory,
            clientId,
            "RS256",
            issuer,
            new RequestObjectOptions { RedirectUri = RedirectUri },
            CancellationToken.None
        );

        using var parResponse = await PushRequestObjectAsync(
            client,
            clientId,
            clientSecret,
            requestObject,
            [new("redirect_uri", "https://client.example/other")],
            CancellationToken.None
        );

        var parBody = await parResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, parResponse.StatusCode);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRequestJwt,
            JsonElement.Parse(parBody).GetProperty("error").GetString()
        );
    }

    #endregion
}
