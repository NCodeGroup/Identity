#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Contains identifying information about a tenant.
/// </summary>
/// <param name="TenantId">Gets or sets the identifier for the tenant.</param>
/// <param name="DisplayName">Gets or sets the display name for the tenant.</param>
/// <param name="DomainName">Gets or sets the domain name for the tenant. This value is optional and can be used to find tenants by domain name.</param>
[PublicAPI]
public readonly record struct TenantDescriptor(
    string TenantId,
    string DisplayName,
    string? DomainName = null
);
