// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using NCode.Buffers;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A <see cref="HostApplicationFactory"/> whose host seeds the bootstrap administrator management client, and that
/// creates management clients acting as it. A test that registers services of its own derives from it.
/// </summary>
/// <param name="configuration">
/// The configuration keys (colon-delimited paths) and values to overlay on the host's configuration.
/// </param>
/// <param name="controlPlaneTenantId">The tenant the bootstrap administrator is seeded in.</param>
public class BootstrapAdminApplicationFactory(
    IReadOnlyDictionary<string, string?> configuration,
    string controlPlaneTenantId = HostApplicationFactory.TenantId
) : HostApplicationFactory
{
    /// <summary>
    /// The client identifier of the bootstrap administrator.
    /// </summary>
    public const string ClientId = "it-bootstrap-admin";

    /// <summary>
    /// The client secret of the bootstrap administrator.
    /// </summary>
    public const string ClientSecret = "bootstrap-secret-value";

    /// <summary>
    /// Initializes a new instance of the <see cref="BootstrapAdminApplicationFactory"/> class that seeds the bootstrap
    /// administrator in the default tenant and overlays no other configuration.
    /// </summary>
    public BootstrapAdminApplicationFactory()
        : this(new Dictionary<string, string?>()) { }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        var secretBytes = SecureEncoding.UTF8.GetBytes(ClientSecret);
        var settings = new Dictionary<string, string?>(configuration, StringComparer.Ordinal)
        {
            ["BootstrapAdmin:ClientId"] = ClientId,
            ["BootstrapAdmin:ClientSecret"] = Base64Url.EncodeToString(secretBytes),
            ["BootstrapAdmin:TenantId"] = controlPlaneTenantId,
        };

        builder.ConfigureAppConfiguration(configurationBuilder =>
            configurationBuilder.AddInMemoryCollection(settings)
        );
    }

    /// <summary>
    /// Gets the token request that the bootstrap administrator sends to the default token endpoint.
    /// </summary>
    public static ManagementTokenRequest TokenRequest => new(ClientId, ClientSecret);

    /// <summary>
    /// Creates an <see cref="HttpClient"/> that calls the management API as the bootstrap administrator.
    /// </summary>
    /// <returns>A task that completes with the authenticated client.</returns>
    public Task<HttpClient> CreateAdministratorClientAsync() =>
        CreateAdministratorClientAsync(CancellationToken.None).AsTask();

    /// <summary>
    /// Creates an <see cref="HttpClient"/> that calls the management API as the bootstrap administrator.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The authenticated client, which does not follow redirects.</returns>
    public ValueTask<HttpClient> CreateAdministratorClientAsync(
        CancellationToken cancellationToken
    ) => TokenRequest.CreateClientAsync(this, cancellationToken);

    /// <summary>
    /// Creates an <see cref="HttpClient"/> that calls the management API as the bootstrap administrator, obtaining
    /// its token from the given token endpoint.
    /// </summary>
    /// <param name="tokenPath">The path of the token endpoint.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The authenticated client, which does not follow redirects.</returns>
    public ValueTask<HttpClient> CreateAdministratorClientAsync(
        string tokenPath,
        CancellationToken cancellationToken
    ) => (TokenRequest with { TokenPath = tokenPath }).CreateClientAsync(this, cancellationToken);
}
