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

using System.Buffers;
using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Extensions.DataProtection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Logic;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Server.Logging;
using NCode.Persistence.Stores;

namespace NCode.Identity.Server;

/// <summary>
/// Seeds the configured bootstrap administrator client into the workload tenant when it is first provisioned,
/// on the same control-plane / tenant provisioning path that seeds the system resource servers. The client's
/// management-audience tokens are stamped with the <c>GlobalAdmin</c> role by
/// <see cref="BootstrapAdminAccessTokenClaimsHandler"/>, so an operator can make the first authorized management call.
/// The operation is idempotent and inert unless a bootstrap client id and secret are configured.
/// </summary>
internal sealed class BootstrapAdminTenantSeeder(
    IOptions<BootstrapAdminOptions> bootstrapOptionsAccessor,
    IOptions<TenantResolutionOptions> tenantOptionsAccessor,
    IStoreManagerFactory storeManagerFactory,
    IAmbientTenantAccessor ambientTenantAccessor,
    IDataProtectorFactory<PersistedSecret> dataProtectorFactory,
    TimeProvider timeProvider,
    ILogger<BootstrapAdminTenantSeeder> logger
) : ISystemTenantSeeder
{
    private BootstrapAdminOptions Options { get; } = bootstrapOptionsAccessor.Value;
    private string RootTenantId { get; } = tenantOptionsAccessor.Value.RootTenantId;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;
    private IDataProtectorFactory<PersistedSecret> DataProtectorFactory { get; } =
        dataProtectorFactory;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<BootstrapAdminTenantSeeder> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask SeedAsync(string tenantId, CancellationToken cancellationToken)
    {
        var clientId = Options.ClientId;
        var clientSecret = Options.ClientSecret;
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            return;
        }

        if (!TargetsTenant(tenantId))
        {
            return;
        }

        using var tenantScope = AmbientTenantAccessor.BeginScope(tenantId);
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var existing = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (existing is not null)
        {
            Logger.BootstrapAdminClientAlreadyExists(tenantId);
            return;
        }

        var persistedClient = CreateClient(tenantId, clientId, clientSecret);

        await store.AddAsync(persistedClient, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        Logger.BootstrapAdminClientSeeded(clientId, tenantId);
    }

    private bool TargetsTenant(string tenantId)
    {
        var configuredTenantId = Options.TenantId;
        if (!string.IsNullOrEmpty(configuredTenantId))
        {
            return string.Equals(tenantId, configuredTenantId, StringComparison.Ordinal);
        }

        // Absent an explicit target, seed into the workload tenant, never the control-plane root tenant.
        return !string.Equals(tenantId, RootTenantId, StringComparison.Ordinal);
    }

    private PersistedClient CreateClient(string tenantId, string clientId, string clientSecret)
    {
        var protector = DataProtectorFactory.CreateDataProtector();
        var secretBytes = Encoding.UTF8.GetBytes(clientSecret);
        var writer = new ArrayBufferWriter<byte>();
        protector.ProtectSpan(secretBytes, ref writer);

        var now = TimeProvider.GetUtcNow();
        var persistedSecret = new PersistedSecret
        {
            SecretId = $"{clientId}-secret",
            Use = null,
            Algorithm = null,
            CreatedWhen = now,
            ExpiresWhen = now.AddYears(100),
            SecretType = SecretTypes.Symmetric,
            KeySizeBits = secretBytes.Length * 8,
            EncodedValue = Base64Url.EncodeToString(writer.WrittenSpan),
        };

        var emptySettings = JsonSerializer.SerializeToElement(new Dictionary<string, object>());

        return new PersistedClient
        {
            TenantId = tenantId,
            ClientId = clientId,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = tenantId,
                ClientId = clientId,
                Value = emptySettings,
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = tenantId,
                ClientId = clientId,
                Value = [persistedSecret],
            },
        };
    }
}
