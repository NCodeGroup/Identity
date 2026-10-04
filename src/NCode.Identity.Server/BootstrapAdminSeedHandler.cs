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
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Logic;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Server.Logging;
using NCode.Mediator;

namespace NCode.Identity.Server;

/// <summary>
/// Seeds the configured bootstrap administrator client into the workload tenant as part of the tenant seed fan-out.
/// The client's management-audience tokens are stamped with the <c>GlobalAdmin</c> role by
/// <see cref="BootstrapAdminAccessTokenClaimsHandler"/>, so an operator can make the first authorized management call.
/// The handler is idempotent and inert unless a bootstrap client id and secret are configured; it never targets the
/// control-plane root tenant.
/// </summary>
internal sealed class BootstrapAdminSeedHandler(
    IOptions<BootstrapAdminOptions> bootstrapOptionsAccessor,
    IDataProtectorFactory<PersistedSecret> dataProtectorFactory,
    TimeProvider timeProvider,
    ILogger<BootstrapAdminSeedHandler> logger
) : ICommandHandler<SeedTenantCommand>, ISupportMediatorPriority
{
    private BootstrapAdminOptions Options { get; } = bootstrapOptionsAccessor.Value;
    private IDataProtectorFactory<PersistedSecret> DataProtectorFactory { get; } =
        dataProtectorFactory;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<BootstrapAdminSeedHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        SeedTenantCommand command,
        CancellationToken cancellationToken
    )
    {
        var clientId = Options.ClientId;
        var clientSecret = Options.ClientSecret;
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            return;
        }

        var context = command.Context;
        if (!TargetsTenant(context))
        {
            return;
        }

        var tenantId = context.TenantId;
        var store = context.StoreManager.GetStore<IClientStore>();

        var existing = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (existing is not null)
        {
            Logger.BootstrapAdminClientAlreadyExists(tenantId);
            return;
        }

        var persistedClient = CreateClient(tenantId, clientId, clientSecret);

        await store.AddAsync(persistedClient, cancellationToken);

        Logger.BootstrapAdminClientSeeded(clientId, tenantId);
    }

    private bool TargetsTenant(TenantSeedContext context)
    {
        // The bootstrap administrator is a workload-plane client; it is never seeded into the control-plane root tenant.
        if (context.Plane != TenantPlane.Workload)
        {
            return false;
        }

        var configuredTenantId = Options.TenantId;
        return string.IsNullOrEmpty(configuredTenantId)
            || string.Equals(context.TenantId, configuredTenantId, StringComparison.Ordinal);
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
