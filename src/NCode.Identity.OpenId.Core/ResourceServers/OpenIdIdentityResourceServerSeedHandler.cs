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

using System.Text.Json;
using NCode.Identity.Logic;
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

        var resourceServer = new PersistedResourceServer
        {
            TenantId = context.TenantId,
            ResourceServerId = CryptoService.GenerateResourceId(),
            Identifier = OpenIdConstants.SystemResourceServerIdentifiers.OpenId,
            ConcurrencyToken = string.Empty,
            Name = "OpenID Connect",
            IsSystem = true,
            IsDisabled = false,
            Settings = JsonSerializer.SerializeToElement(null, typeof(object)),
            Scopes = BuildScopes(),
        };

        await store.AddAsync(resourceServer, cancellationToken);
    }

    private static List<PersistedScope> BuildScopes() =>
        [
            Scope(OpenIdConstants.ScopeTypes.OpenId, "Sign you in and issue an identity token."),
            Scope(OpenIdConstants.ScopeTypes.Profile, "Access your profile information."),
            Scope(OpenIdConstants.ScopeTypes.Email, "Access your email address."),
            Scope(OpenIdConstants.ScopeTypes.Address, "Access your postal address."),
            Scope(OpenIdConstants.ScopeTypes.Phone, "Access your phone number."),
            Scope(
                OpenIdConstants.ScopeTypes.OfflineAccess,
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
