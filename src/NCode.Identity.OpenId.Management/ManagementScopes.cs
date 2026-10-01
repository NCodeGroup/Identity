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
    /// Contains the resource-family segments of a management scope value.
    /// </summary>
    internal static class Families
    {
        internal const string Clients = "clients";
        internal const string ResourceServers = "resource_servers";
        internal const string ClientGrants = "client_grants";
        internal const string Grants = "grants";
        internal const string Servers = "servers";
        internal const string Tenants = "tenants";
    }

    /// <summary>
    /// Composes a scope value from a verb and a resource family.
    /// </summary>
    internal static string For(string verb, string family) => $"{verb}:{family}";
}
