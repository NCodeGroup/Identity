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

namespace NCode.Identity.OpenId.Management.ResourceServers;

/// <summary>
/// Seeds the reserved management resource server (<c>urn:ncode:management</c>) into a tenant. Its tenant-plane scope
/// families (a tenant's own settings and secrets, plus its clients, client secrets, resource servers, client grants,
/// and grants) are seeded into every tenant; its control-plane families (server management and tenant provisioning)
/// are added only when seeding the root tenant. The handler is idempotent: it creates the resource server only when it
/// is absent.
/// </summary>
internal sealed class ManagementResourceServerSeedHandler(ICryptoService cryptoService)
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
            OpenIdConstants.SystemResourceServerIdentifiers.Management,
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
            Identifier = OpenIdConstants.SystemResourceServerIdentifiers.Management,
            ConcurrencyToken = string.Empty,
            Name = "Management",
            IsSystem = true,
            IsDisabled = false,
            Settings = JsonSerializer.SerializeToElement(null, typeof(object)),
            Scopes = BuildScopes(context.Plane),
        };

        await store.AddAsync(resourceServer, cancellationToken);
    }

    private static List<PersistedScope> BuildScopes(TenantPlane plane)
    {
        IEnumerable<PersistedScope> scopes =
        [
            // Tenant-plane families, seeded into every tenant.
            .. SettingsScopes(ManagementScopes.Families.TenantSettings, "tenant settings"),
            .. CrudScopes(ManagementScopes.Families.TenantSecrets, "tenant secrets"),
            .. CrudScopes(ManagementScopes.Families.Clients, "clients"),
            .. CrudScopes(ManagementScopes.Families.ClientSecrets, "client secrets"),
            .. CrudScopes(ManagementScopes.Families.ResourceServers, "resource servers"),
            .. CrudScopes(ManagementScopes.Families.ClientGrants, "client grants"),
            .. CrudScopes(ManagementScopes.Families.LocalAccounts, "local accounts"),
            Scope(
                ManagementScopes.For(ManagementScopes.Verbs.Read, ManagementScopes.Families.Grants),
                "Read grants (user authorizations)."
            ),
            Scope(
                ManagementScopes.For(
                    ManagementScopes.Verbs.Delete,
                    ManagementScopes.Families.Grants
                ),
                "Revoke grants (user authorizations)."
            ),
        ];

        if (plane == TenantPlane.Root)
        {
            // Control-plane families, seeded only into the root tenant.
            scopes = scopes.Concat([
                .. CrudScopes(ManagementScopes.Families.Servers, "servers"),
                .. SettingsScopes(ManagementScopes.Families.ServerSettings, "server settings"),
                .. CrudScopes(ManagementScopes.Families.ServerSecrets, "server secrets"),
                .. CrudScopes(ManagementScopes.Families.Tenants, "tenants"),
            ]);
        }

        return scopes
            .GroupBy(scope => scope.Value, StringComparer.Ordinal)
            .Select(byValue => byValue.First())
            .ToList();
    }

    private static IEnumerable<PersistedScope> CrudScopes(string family, string noun) =>
        [
            Scope(ManagementScopes.For(ManagementScopes.Verbs.Read, family), $"Read {noun}."),
            Scope(ManagementScopes.For(ManagementScopes.Verbs.Create, family), $"Create {noun}."),
            Scope(ManagementScopes.For(ManagementScopes.Verbs.Update, family), $"Update {noun}."),
            Scope(ManagementScopes.For(ManagementScopes.Verbs.Delete, family), $"Delete {noun}."),
        ];

    private static IEnumerable<PersistedScope> SettingsScopes(string family, string noun) =>
        [
            Scope(ManagementScopes.For(ManagementScopes.Verbs.Read, family), $"Read {noun}."),
            Scope(ManagementScopes.For(ManagementScopes.Verbs.Update, family), $"Update {noun}."),
        ];

    private static PersistedScope Scope(string value, string description) =>
        new()
        {
            Value = value,
            Description = description,
            IsSystem = true,
        };
}
