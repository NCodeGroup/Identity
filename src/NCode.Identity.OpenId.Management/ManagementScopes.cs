// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Management;

/// <summary>
/// Contains the scope values that gate the management API, composed as <c>{verb}:{family}</c> (the <c>List</c> and
/// <c>Read</c> operations collapse to the <c>read</c> verb).
/// </summary>
internal static class ManagementScopes
{
    /// <summary>
    /// Contains the verb segments of a management scope value.
    /// </summary>
    internal static class Verbs
    {
        internal const string Read = "read";
        internal const string Create = "create";
        internal const string Update = "update";
        internal const string Delete = "delete";
    }

    /// <summary>
    /// Contains the resource-family segments of a management scope value. A family is an entity or one of its leaves
    /// (settings, secrets), so that roles can be composed at per-leaf granularity.
    /// </summary>
    internal static class Families
    {
        internal const string Servers = "servers";
        internal const string ServerSettings = "server_settings";
        internal const string ServerSecrets = "server_secrets";
        internal const string Tenants = "tenants";
        internal const string TenantSettings = "tenant_settings";
        internal const string TenantSecrets = "tenant_secrets";
        internal const string Clients = "clients";
        internal const string ClientSecrets = "client_secrets";
        internal const string ResourceServers = "resource_servers";
        internal const string ResourceServerSecrets = "resource_server_secrets";
        internal const string ClientGrants = "client_grants";
        internal const string Grants = "grants";
        internal const string LocalAccounts = "local_accounts";
    }

    /// <summary>
    /// Composes a scope value from a verb and a resource family.
    /// </summary>
    internal static string For(string verb, string family) => $"{verb}:{family}";
}
