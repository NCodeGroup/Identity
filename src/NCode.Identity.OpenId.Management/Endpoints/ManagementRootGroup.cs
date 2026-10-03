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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Endpoints;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// The root management route group (<c>/api</c>). It materializes the OpenID request environment for every management
/// endpoint via <see cref="ManagementEnvironmentEndpointFilter"/> but opens no tenant scope, so control-plane
/// families (servers, tenant provisioning) act on unscoped rows (ADR-0042).
/// </summary>
internal sealed class ManagementRootGroup : IEndpointGroup
{
    /// <summary>
    /// The <see cref="IEndpointGroup.Name"/> of the root management group.
    /// </summary>
    public const string GroupName = "api";

    /// <inheritdoc />
    public string Name => GroupName;

    /// <inheritdoc />
    public string? ParentName => null;

    /// <inheritdoc />
    public string Prefix => "/api";

    /// <inheritdoc />
    public void Configure(RouteGroupBuilder group) =>
        group.AddEndpointFilter<RouteGroupBuilder, ManagementEnvironmentEndpointFilter>();
}
