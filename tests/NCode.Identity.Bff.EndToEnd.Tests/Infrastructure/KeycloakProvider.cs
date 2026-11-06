// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using NCode.Buffers;
using NCode.Identity.OpenId;

namespace NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;

/// <summary>
/// Keycloak running in a container, as the OpenID Provider that the end-to-end suites run against.
/// </summary>
public sealed class KeycloakProvider : IAsyncDisposable
{
    private const int HttpsPort = 8443;
    private const string CertificatePath = "/opt/keycloak/conf/e2e-tls.crt";
    private const string PrivateKeyPath = "/opt/keycloak/conf/e2e-tls.key";
    private const string AdminClientId = "e2e-admin";
    private const int LogLinesOnFailure = 120;

    /// <summary>
    /// The name of the realm that the container imports.
    /// </summary>
    public const string RealmName = "bff-e2e";

    /// <summary>
    /// The user name of the realm's test user.
    /// </summary>
    public const string UserName = "alice";

    /// <summary>
    /// The email address of the realm's test user.
    /// </summary>
    public const string UserEmail = "alice@example.com";

    private readonly TestCertificate _certificate;
    private readonly IFutureDockerImage _image;
    private readonly IContainer _container;
    private readonly string _adminSecret;
    private readonly int _port;

    private KeycloakProvider(
        TestCertificate certificate,
        IFutureDockerImage image,
        IContainer container,
        string adminSecret,
        string userPassword
    )
    {
        _certificate = certificate;
        _image = image;
        _container = container;
        _adminSecret = adminSecret;
        _port = container.GetMappedPublicPort(HttpsPort);
        UserPassword = userPassword;
    }

    /// <summary>
    /// Gets the issuer of the realm, which a browser and the host reach through the mapped port.
    /// </summary>
    public string Issuer => $"https://127.0.0.1:{_port}/realms/{RealmName}";

    /// <summary>
    /// Gets the password of the realm's test user, which is random for each run.
    /// </summary>
    public string UserPassword { get; }

    /// <summary>
    /// Builds the image and starts a container that imports the realm.
    /// </summary>
    public static async Task<KeycloakProvider> StartAsync(
        TestCertificate certificate,
        IReadOnlyDictionary<string, string> realmVariables
    )
    {
        var userPassword = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(24));
        var adminSecret = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(24));

        // The build context is copied to the output: a CI build maps source paths, so the project directory can't be
        // found from them.
        var image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(Path.Combine(AppContext.BaseDirectory, "Keycloak"))
            .WithDockerfile("Dockerfile")
            .WithDeleteIfExists(true)
            .Build();

        await image.CreateAsync();

        var builder = new ContainerBuilder(image)
            .WithCommand("start-dev", "--import-realm")
            .WithPortBinding(HttpsPort, true)
            .WithCreateParameterModifier(parameters =>
            {
                // The container is reachable from loopback only.
                var hostConfig = parameters.HostConfig ??= new HostConfig();
                hostConfig.PortBindings ??= new Dictionary<string, IList<PortBinding>>();
                hostConfig.PortBindings[$"{HttpsPort}/tcp"] =
                [
                    new PortBinding { HostIP = "127.0.0.1", HostPort = "0" },
                ];
            })
            .WithEnvironment("KC_HTTP_ENABLED", "false")
            .WithEnvironment("KC_HTTPS_CERTIFICATE_FILE", CertificatePath)
            .WithEnvironment("KC_HTTPS_CERTIFICATE_KEY_FILE", PrivateKeyPath)
            .WithEnvironment("KC_TRUSTSTORE_PATHS", CertificatePath)
            // Token verification logs why it rejects a token only at debug.
            .WithEnvironment(
                "KC_LOG_LEVEL",
                "INFO,org.keycloak.services:DEBUG,org.keycloak.protocol.oidc.TokenManager:DEBUG"
            )
            .WithEnvironment("E2E_USER_PASSWORD", userPassword)
            .WithEnvironment("E2E_ADMIN_SECRET", adminSecret)
            .WithResourceMapping(
                SecureEncoding.UTF8.GetBytes(certificate.CertificatePem),
                CertificatePath
            )
            .WithResourceMapping(
                SecureEncoding.UTF8.GetBytes(certificate.PrivateKeyPem),
                PrivateKeyPath
            )
            .WithWaitStrategy(
                Wait.ForUnixContainer().UntilMessageIsLogged("Listening on: .*https://")
            );

        foreach (var (name, value) in realmVariables)
        {
            builder = builder.WithEnvironment(name, value);
        }

        var container = builder.Build();

        try
        {
            await container.StartAsync();
        }
        catch
        {
            await container.DisposeAsync();
            await image.DisposeAsync();
            throw;
        }

        return new KeycloakProvider(certificate, image, container, adminSecret, userPassword);
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> that trusts only the test certificate.
    /// </summary>
    public HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler();
        handler.SslOptions.RemoteCertificateValidationCallback = (_, presented, _, _) =>
            _certificate.IsTrusted(presented);

        return new HttpClient(handler, disposeHandler: true);
    }

    /// <summary>
    /// Ends every session of the test user, which makes Keycloak send the back-channel logout requests.
    /// </summary>
    public async Task LogOutUserAsync()
    {
        using var client = CreateHttpClient();
        var adminBase = $"https://127.0.0.1:{_port}/admin/realms/{RealmName}";

        using var tokenRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{Issuer}/protocol/openid-connect/token"
        )
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    [OpenIdProtocol.Parameters.GrantType] = OpenIdProtocol
                        .GrantTypes
                        .ClientCredentials,
                }
            ),
        };
        tokenRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(SecureEncoding.UTF8.GetBytes($"{AdminClientId}:{_adminSecret}"))
        );

        using var tokenResponse = await client.SendAsync(tokenRequest);
        await EnsureSuccessAsync(tokenResponse, accessToken: null);
        var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>(
            JsonSerializerOptions.Web
        );
        if (token is null)
            throw new InvalidOperationException("The token response was empty.");

        using var usersRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{adminBase}/users?username={UserName}&exact=true"
        );
        usersRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.AccessToken
        );

        using var usersResponse = await client.SendAsync(usersRequest);
        await EnsureSuccessAsync(usersResponse, token.AccessToken);
        var users = await usersResponse.Content.ReadFromJsonAsync<List<UserRepresentation>>(
            JsonSerializerOptions.Web
        );
        if (users is not [var user])
            throw new InvalidOperationException($"Expected exactly one user named '{UserName}'.");

        using var logoutRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{adminBase}/users/{user.Id}/logout"
        );
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.AccessToken
        );

        using var logoutResponse = await client.SendAsync(logoutRequest);
        await EnsureSuccessAsync(logoutResponse, token.AccessToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
        await _image.DisposeAsync();
    }

    // Keycloak's answer to a refused call names no reason, so the failure carries the token's claims and the end of
    // Keycloak's log, which does.
    private async Task EnsureSuccessAsync(HttpResponseMessage response, string? accessToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync();
        var (stdout, stderr) = await _container.GetLogsAsync(timestampsEnabled: false);
        var log = string.Join('\n', $"{stdout}{stderr}".Split('\n').TakeLast(LogLinesOnFailure));
        var request = response.RequestMessage;
        throw new HttpRequestException(
            $"Keycloak answered {(int)response.StatusCode} to {request?.Method} {request?.RequestUri}. "
                + $"WWW-Authenticate: '{response.Headers.WwwAuthenticate}'. Body: '{body}'. "
                + $"Token claims: {DescribeToken(accessToken)}. Now: {TimeProvider.System.GetUtcNow().ToUnixTimeSeconds()}."
                + $"\nKeycloak log:\n{log}",
            inner: null,
            response.StatusCode
        );
    }

    private static string DescribeToken(string? accessToken)
    {
        if (accessToken?.Split('.') is not [_, var payload, _])
            return "none";

        var claims = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            Base64Url.DecodeFromChars(payload),
            JsonSerializerOptions.Web
        );
        string[] names =
        [
            "iss",
            "azp",
            "sub",
            "aud",
            "scope",
            "resource_access",
            "typ",
            "iat",
            "exp",
            "sid",
        ];
        return string.Join(
            ", ",
            names.Select(name =>
                claims is not null && claims.TryGetValue(name, out var value)
                    ? $"{name}={value}"
                    : $"{name} absent"
            )
        );
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken
    );

    private sealed record UserRepresentation(string Id);
}
