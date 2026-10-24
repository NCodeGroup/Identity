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

using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Tenants;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The route group for the single-page bundle's content-hashed files. Its prefix is the tenant route, like the
/// interactive surface's group, because the files sit beneath the surface of the tenant whose page loads them, where
/// the page's base resolves their relative URLs. A content-hashed file is the same for every tenant, so unlike the
/// surface's group this one adds no environment filter: serving a file resolves no tenant.
/// </summary>
// See ADR-0254.
internal sealed class InteractiveBundleAssetEndpointGroup : IEndpointGroup
{
    /// <summary>
    /// The <see cref="IEndpointGroup.Name"/> of the bundle's asset group.
    /// </summary>
    public const string GroupName = "interactive-bundle-assets";

    /// <inheritdoc />
    public string Name => GroupName;

    /// <inheritdoc />
    public RoutePattern GetPrefix(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<ITenantResolver>().GetTenantRoute();

    /// <inheritdoc />
    public void ConfigureServices(IEndpointGroupBuilder builder) =>
        builder.AddEndpoint<InteractiveBundleAssetEndpointHandler>();
}
