﻿#region Copyright Preamble

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
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Options;

/// <summary>
/// Contains the options for dynamic tenant resolution based on the path.
/// </summary>
[PublicAPI]
public class DynamicByPathOpenIdTenantOptions
{
    /// <summary>
    /// Contains the name of the route parameter that will be used to determine the tenant identifier.
    /// This value must be contained within the <see cref="TenantPath"/> route pattern.
    /// The default value is 'tenantId'.
    /// </summary>
    public string TenantIdRouteParameterName { get; set; } = "tenantId";

    /// <summary>
    /// Gets or sets the relative base path for the tenant.
    /// This value must be set to a route pattern that includes the tenant identifier as a route parameter with a leading slash.
    /// The default value is '/{tenantId}'.
    /// </summary>
    public PathString TenantPath { get; set; } = "/{tenantId}";
}
