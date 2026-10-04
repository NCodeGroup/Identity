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

using NCode.Identity.OpenId.ResourceServers;

namespace NCode.Identity.OpenId.Management.ResourceServers;

/// <summary>
/// Contributes the tenant-plane scope families of the reserved management resource server (<c>urn:ncode:management</c>),
/// seeded into every tenant: a tenant's own settings and secrets, plus its clients, client secrets, resource servers,
/// client grants, and grants. The control-plane provisioning families (servers and tenants) are contributed separately
/// and seeded only into the root tenant.
/// </summary>
internal class ManagementResourceServerProvider : ISystemResourceServerProvider
{
    /// <inheritdoc />
    public SystemResourceServerDescriptor GetDescriptor() =>
        new()
        {
            Identifier = OpenIdConstants.SystemResourceServerIdentifiers.Management,
            Name = "Management",
            Scopes =
            [
                .. SettingsScopes(ManagementScopes.Families.TenantSettings, "tenant settings"),
                .. CrudScopes(ManagementScopes.Families.TenantSecrets, "tenant secrets"),
                .. CrudScopes(ManagementScopes.Families.Clients, "clients"),
                .. CrudScopes(ManagementScopes.Families.ClientSecrets, "client secrets"),
                .. CrudScopes(ManagementScopes.Families.ResourceServers, "resource servers"),
                .. CrudScopes(ManagementScopes.Families.ClientGrants, "client grants"),
                new SystemScopeDescriptor
                {
                    Value = ManagementScopes.For(
                        ManagementScopes.Verbs.Read,
                        ManagementScopes.Families.Grants
                    ),
                    Description = "Read grants (user authorizations).",
                },
                new SystemScopeDescriptor
                {
                    Value = ManagementScopes.For(
                        ManagementScopes.Verbs.Delete,
                        ManagementScopes.Families.Grants
                    ),
                    Description = "Revoke grants (user authorizations).",
                },
            ],
        };

    private static IEnumerable<SystemScopeDescriptor> CrudScopes(string family, string noun) =>
        [
            new SystemScopeDescriptor
            {
                Value = ManagementScopes.For(ManagementScopes.Verbs.Read, family),
                Description = $"Read {noun}.",
            },
            new SystemScopeDescriptor
            {
                Value = ManagementScopes.For(ManagementScopes.Verbs.Create, family),
                Description = $"Create {noun}.",
            },
            new SystemScopeDescriptor
            {
                Value = ManagementScopes.For(ManagementScopes.Verbs.Update, family),
                Description = $"Update {noun}.",
            },
            new SystemScopeDescriptor
            {
                Value = ManagementScopes.For(ManagementScopes.Verbs.Delete, family),
                Description = $"Delete {noun}.",
            },
        ];

    private static IEnumerable<SystemScopeDescriptor> SettingsScopes(string family, string noun) =>
        [
            new SystemScopeDescriptor
            {
                Value = ManagementScopes.For(ManagementScopes.Verbs.Read, family),
                Description = $"Read {noun}.",
            },
            new SystemScopeDescriptor
            {
                Value = ManagementScopes.For(ManagementScopes.Verbs.Update, family),
                Description = $"Update {noun}.",
            },
        ];
}
