// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Tenants.Keys;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Drives a host configured the way production runs it, no developer keys, through a key rollover: a provisioned key
/// that nears its retirement gets a successor published ahead of the hand-over and keeps signing until then, and an
/// expired key leaves the key set and is replaced. The management API shows every key's lifecycle, and the audit
/// trail records what the server changed.
/// </summary>
public class TenantKeyRolloverTests
{
    private const string JwksPath = "/oauth2/jwks";
    private const string SecretsPath = $"/api/tenants/{HostApplicationFactory.TenantId}/secrets";

    [Fact]
    public async Task PutSecret_WhenTheActiveKeyRetiresSoon_PublishesItsSuccessorAheadAndKeepsSigningWithIt()
    {
        var recorder = new RecordingAuditEventHandler();
        using var factory = new KeyRolloverAuditHostApplicationFactory(recorder);
        using var administrator = await CreateAdministratorClientAsync(factory);
        var client = CreateHttpClient(factory);

        var provisioned = await GetSecretsAsync(administrator);
        var rs256 = Assert.Single(provisioned, secret => secret.Algorithm == "RS256");
        Assert.Equal(3, provisioned.Count);
        Assert.Equal(rs256.CreatedWhen, rs256.ActivatesWhen);
        Assert.Equal(rs256.CreatedWhen.AddDays(90), rs256.RetiresWhen);
        Assert.Equal(rs256.RetiresWhen.AddDays(7), rs256.ExpiresWhen);
        Assert.Equal(SecretManagers.TenantKeyRollover, rs256.ManagedBy);

        // The key retires in an hour, within the activation delay and the pass interval, so the seed that follows the
        // change generates its successor, published now and activating at the hand-over. The host reads the system
        // clock, so the retirement is set from it; every instant the test asserts is derived from the stored records,
        // never from the clock, and the hour is the margin the test allows itself to reach the next request.
        var retiresWhen = DateTimeOffset.UtcNow.AddHours(1);
        await UpdateLifecycleAsync(
            administrator,
            rs256,
            activatesWhen: rs256.ActivatesWhen,
            retiresWhen: retiresWhen,
            expiresWhen: retiresWhen.AddDays(7)
        );

        var keys = await GetJwksAsync(client);
        var rs256Keys = keys.Where(key => key.GetProperty("alg").GetString() == "RS256").ToList();
        Assert.Equal(2, rs256Keys.Count);
        Assert.Equal(4, keys.Count);

        var secrets = await GetSecretsAsync(administrator);
        var successor = Assert.Single(
            secrets,
            secret => secret.Algorithm == "RS256" && secret.SecretId != rs256.SecretId
        );
        Assert.Equal(retiresWhen, successor.ActivatesWhen);
        Assert.Equal(retiresWhen.AddDays(90), successor.RetiresWhen);
        Assert.Equal(successor.RetiresWhen.AddDays(7), successor.ExpiresWhen);
        Assert.Contains(rs256Keys, key => key.GetProperty("kid").GetString() == successor.SecretId);

        // The successor is published but not active, so the retiring key still signs.
        var accessToken = await GetBootstrapTokenAsync(client);
        Assert.Equal(rs256.SecretId, DecodeJwtHeader(accessToken).GetProperty("kid").GetString());

        var generated = Assert.Single(
            recorder.Events.OfType<TenantKeyAuditEvent>(),
            auditEvent =>
                auditEvent.Change == TenantKeyChanges.Generated
                && auditEvent.Reason == TenantKeyChangeReasons.Successor
        );
        Assert.Equal(successor.SecretId, generated.SecretId);
        Assert.Equal(HostApplicationFactory.TenantId, generated.TenantId);
        Assert.Equal(AuditActorTypes.System, generated.Actor.Type);
    }

    [Fact]
    public async Task PutSecret_WhenAKeyHasExpired_RemovesItFromTheKeySetAndReplacesIt()
    {
        var recorder = new RecordingAuditEventHandler();
        using var factory = new KeyRolloverAuditHostApplicationFactory(recorder);
        using var administrator = await CreateAdministratorClientAsync(factory);
        var client = CreateHttpClient(factory);

        var provisioned = await GetSecretsAsync(administrator);
        var es256 = Assert.Single(provisioned, secret => secret.Algorithm == "ES256");

        // The key's whole lifecycle collapses onto its creation, which lies in the past, so it has expired: the seed
        // that follows the change removes it, since the server generated it, and generates a replacement that
        // activates at once.
        await UpdateLifecycleAsync(
            administrator,
            es256,
            activatesWhen: es256.CreatedWhen,
            retiresWhen: es256.CreatedWhen,
            expiresWhen: es256.CreatedWhen
        );

        var keys = await GetJwksAsync(client);
        var es256Key = Assert.Single(keys, key => key.GetProperty("alg").GetString() == "ES256");
        Assert.NotEqual(es256.SecretId, es256Key.GetProperty("kid").GetString());
        Assert.Equal(3, keys.Count);

        var secrets = await GetSecretsAsync(administrator);
        Assert.DoesNotContain(secrets, secret => secret.SecretId == es256.SecretId);
        var replacement = Assert.Single(secrets, secret => secret.Algorithm == "ES256");
        Assert.Equal(replacement.CreatedWhen, replacement.ActivatesWhen);

        var events = recorder.Events.OfType<TenantKeyAuditEvent>().ToList();
        var removed = Assert.Single(
            events,
            auditEvent => auditEvent.Change == TenantKeyChanges.Removed
        );
        Assert.Equal(es256.SecretId, removed.SecretId);
        Assert.Equal(TenantKeyChangeReasons.Expired, removed.Reason);
        var generated = Assert.Single(
            events,
            auditEvent =>
                auditEvent.Change == TenantKeyChanges.Generated
                && auditEvent.SecretId == replacement.SecretId
        );
        Assert.Equal(TenantKeyChangeReasons.NoActiveKey, generated.Reason);
    }

    [Fact]
    public async Task PutSecret_WhenTheLifecycleIsOutOfOrder_AnswersBadRequest()
    {
        using var factory = new KeyRolloverAuditHostApplicationFactory(
            new RecordingAuditEventHandler()
        );
        using var administrator = await CreateAdministratorClientAsync(factory);

        var provisioned = await GetSecretsAsync(administrator);
        var rs256 = Assert.Single(provisioned, secret => secret.Algorithm == "RS256");

        using var response = await administrator.PutAsJsonAsync(
            $"{SecretsPath}/{rs256.SecretId}",
            new
            {
                use = rs256.Use,
                algorithm = rs256.Algorithm,
                expiresWhen = rs256.ExpiresWhen,
                activatesWhen = rs256.ExpiresWhen.AddDays(1),
            }
        );
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        Assert.Contains("cannot retire", body, StringComparison.Ordinal);
    }

    private sealed record SecretView(
        string SecretId,
        string? Use,
        string? Algorithm,
        DateTimeOffset CreatedWhen,
        DateTimeOffset ActivatesWhen,
        DateTimeOffset RetiresWhen,
        DateTimeOffset ExpiresWhen,
        string ConcurrencyToken,
        string? ManagedBy
    );

    private static async Task<List<SecretView>> GetSecretsAsync(HttpClient administrator)
    {
        using var response = await administrator.GetAsync(SecretsPath);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        return
        [
            .. JsonElement
                .Parse(body)
                .GetProperty("secrets")
                .EnumerateArray()
                .Select(secret => new SecretView(
                    secret.GetProperty("secretId").GetString() ?? string.Empty,
                    secret.GetProperty("use").GetString(),
                    secret.GetProperty("algorithm").GetString(),
                    secret.GetProperty("createdWhen").GetDateTimeOffset(),
                    secret.GetProperty("activatesWhen").GetDateTimeOffset(),
                    secret.GetProperty("retiresWhen").GetDateTimeOffset(),
                    secret.GetProperty("expiresWhen").GetDateTimeOffset(),
                    secret.GetProperty("concurrencyToken").GetString() ?? string.Empty,
                    secret.GetProperty("managedBy").GetString()
                )),
        ];
    }

    private static async Task UpdateLifecycleAsync(
        HttpClient administrator,
        SecretView secret,
        DateTimeOffset activatesWhen,
        DateTimeOffset retiresWhen,
        DateTimeOffset expiresWhen
    )
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"{SecretsPath}/{secret.SecretId}"
        )
        {
            Content = JsonContent.Create(
                new
                {
                    use = secret.Use,
                    algorithm = secret.Algorithm,
                    activatesWhen,
                    retiresWhen,
                    expiresWhen,
                }
            ),
        };
        request.Headers.TryAddWithoutValidation("If-Match", secret.ConcurrencyToken);

        using var response = await administrator.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, body);
    }

    private static async Task<List<JsonElement>> GetJwksAsync(HttpClient client)
    {
        using var response = await client.GetAsync(JwksPath);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var jsonElement = JsonElement.Parse(body);
        return [.. jsonElement.GetProperty("keys").EnumerateArray().Select(key => key.Clone())];
    }

    private static HttpClient CreateHttpClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<HttpClient> CreateAdministratorClientAsync(
        HostApplicationFactory factory
    )
    {
        var client = CreateHttpClient(factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await GetBootstrapTokenAsync(client)
        );
        return client;
    }

    // The bootstrap client registers client_secret_basic, so it presents its secret with HTTP Basic authentication.
    private static async Task<string> GetBootstrapTokenAsync(HttpClient client)
    {
        var credentials = SecureEncoding.UTF8.GetBytes(
            $"{BootstrapAdminApplicationFactory.ClientId}:{BootstrapAdminApplicationFactory.ClientSecret}"
        );
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
        {
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(credentials)
                ),
            },
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                    ["scope"] = "read:clients",
                }
            ),
        };

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var jsonElement = JsonElement.Parse(body);
        return jsonElement.GetProperty("access_token").GetString() ?? string.Empty;
    }

    private static JsonElement DecodeJwtHeader(string jwt) =>
        JsonElement.Parse(Base64Url.DecodeFromChars(jwt.Split('.')[0]));

    /// <summary>
    /// A <see cref="BootstrapAdminApplicationFactory"/> whose host also sends its audit events to a recorder.
    /// </summary>
    private sealed class KeyRolloverAuditHostApplicationFactory(RecordingAuditEventHandler recorder)
        : BootstrapAdminApplicationFactory()
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
                services.AddSingleton<IEventHandler<IAuditEvent>>(recorder)
            );
        }
    }
}
