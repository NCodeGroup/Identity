// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Core.ResourceServers;

/// <summary>
/// Seeds the reserved system resource server for the standard OpenID Connect identity scopes (<c>urn:ncode:openid</c>)
/// into every tenant. The handler is idempotent: it creates the resource server only when it is absent.
/// </summary>
internal sealed class OpenIdIdentityResourceServerSeedHandler(ICryptoService cryptoService)
    : ICommandHandler<SeedTenantCommand>,
        ISupportMediatorPriority
{
    private ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        SeedTenantCommand command,
        CancellationToken cancellationToken
    )
    {
        var context = command.Context;
        var store = context.StoreManager.GetStore<IResourceServerStore>();

        var existing = await store.GetByIdentifierOrDefaultAsync(
            OpenIdConstants.SystemResourceServerIdentifiers.OpenId,
            cancellationToken
        );
        if (existing is not null)
        {
            return;
        }

        var resourceServerId = CryptoService.GenerateResourceId();

        var resourceServer = new PersistedResourceServer
        {
            TenantId = context.TenantId,
            ResourceServerId = resourceServerId,
            Identifier = OpenIdConstants.SystemResourceServerIdentifiers.OpenId,
            ConcurrencyToken = string.Empty,
            Name = "OpenID Connect",
            IsSystem = true,
            IsDisabled = false,
            Settings = new PersistedResourceServerSettings
            {
                TenantId = context.TenantId,
                ResourceServerId = resourceServerId,
                Value = JsonSerializer.SerializeToElement(null, typeof(object)),
            },
            Scopes = BuildScopes(),
            Secrets = new PersistedResourceServerSecrets
            {
                TenantId = context.TenantId,
                ResourceServerId = resourceServerId,
                Value = [],
            },
        };

        await store.AddAsync(resourceServer, cancellationToken);
    }

    private static List<PersistedScope> BuildScopes() =>
        [
            Scope(OpenIdProtocol.ScopeTypes.OpenId, "Sign you in and issue an identity token."),
            Scope(OpenIdProtocol.ScopeTypes.Profile, "Access your profile information."),
            Scope(OpenIdProtocol.ScopeTypes.Email, "Access your email address."),
            Scope(OpenIdProtocol.ScopeTypes.Address, "Access your postal address."),
            Scope(OpenIdProtocol.ScopeTypes.Phone, "Access your phone number."),
            Scope(
                OpenIdProtocol.ScopeTypes.OfflineAccess,
                "Maintain access while you are offline (refresh tokens)."
            ),
        ];

    private static PersistedScope Scope(string value, string description) =>
        new()
        {
            Value = value,
            Description = description,
            IsSystem = true,
        };
}
