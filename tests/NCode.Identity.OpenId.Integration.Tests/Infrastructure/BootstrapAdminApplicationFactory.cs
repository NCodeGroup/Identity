// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using NCode.Buffers;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A <see cref="HostApplicationFactory"/> whose host seeds the bootstrap administrator management client, and that
/// creates management clients acting as it.
/// </summary>
public sealed class BootstrapAdminApplicationFactory : HostApplicationFactory
{
    /// <summary>
    /// The client identifier of the bootstrap administrator.
    /// </summary>
    public const string ClientId = "it-bootstrap-admin";

    /// <summary>
    /// The client secret of the bootstrap administrator.
    /// </summary>
    public const string ClientSecret = "bootstrap-secret-value";

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration(configurationBuilder =>
            configurationBuilder.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["BootstrapAdmin:ClientId"] = ClientId,
                    ["BootstrapAdmin:ClientSecret"] = Base64Url.EncodeToString(
                        SecureEncoding.UTF8.GetBytes(ClientSecret)
                    ),
                    ["BootstrapAdmin:TenantId"] = TenantId,
                }
            )
        );
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> that calls the management API as the bootstrap administrator.
    /// </summary>
    /// <returns>A task that completes with the authenticated client.</returns>
    public Task<HttpClient> CreateAdministratorClientAsync() =>
        CreateManagementClientAsync(ClientId, ClientSecret);
}
