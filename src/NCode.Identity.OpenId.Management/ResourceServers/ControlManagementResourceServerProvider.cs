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
/// Contributes the control-plane scope families of the reserved management resource server (<c>urn:ncode:management</c>),
/// seeded only into the root tenant: server management (and its settings and secrets) and tenant provisioning
/// (create/delete/enumerate tenants). The tenant-plane families belong to <see cref="ManagementResourceServerProvider"/>;
/// the seeder merges both into one resource server per tenant.
/// </summary>
internal class ControlManagementResourceServerProvider : ISystemResourceServerProvider
{
    /// <inheritdoc />
    public SystemResourceServerDescriptor GetDescriptor() =>
        new()
        {
            Identifier = OpenIdConstants.SystemResourceServerIdentifiers.Management,
            Name = "Management",
            Plane = SystemResourceServerPlane.Control,
            Scopes =
            [
                .. CrudScopes(ManagementScopes.Families.Servers, "servers"),
                .. SettingsScopes(ManagementScopes.Families.ServerSettings, "server settings"),
                .. CrudScopes(ManagementScopes.Families.ServerSecrets, "server secrets"),
                .. CrudScopes(ManagementScopes.Families.Tenants, "tenants"),
            ],
        };

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
}
