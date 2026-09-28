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
/// Provides constants for built-in role names used in the authorization system.
/// </summary>
public static class BuiltInRoles
{
    /// <summary>
    /// The role name for global administrators who have full access across all tenants and resources.
    /// </summary>
    public const string GlobalAdmin = "GlobalAdmin";

    /// <summary>
    /// The role name for tenant administrators who have administrative access within a specific tenant.
    /// </summary>
    public const string TenantAdmin = "TenantAdmin";
}
