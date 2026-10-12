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
using NCode.Identity.OpenId.Contexts;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The route group for the interactive login/consent/account surface. An empty prefix preserves each endpoint's
/// absolute route (for example <c>/account/login</c>) while still grouping them so the shared
/// <see cref="OpenIdEnvironmentEndpointFilter"/> — which materializes the request environment and opens the resolved
/// tenant's ambient scope the credential verifier reads — applies only to these endpoints. Unlike the protocol group,
/// it adds no exception filter: the interactive endpoints render HTML pages and redirects, not OpenID error responses.
/// It mirrors the protocol endpoints' registration so the surface flows through the same
/// <c>MapEndpointGroups</c> discovery as every other endpoint instead of a bespoke mapping call.
/// </summary>
internal sealed class InteractiveAuthenticationEndpointGroup : IEndpointGroup
{
    /// <summary>
    /// The <see cref="IEndpointGroup.Name"/> of the interactive surface group.
    /// </summary>
    public const string GroupName = "interactive-authentication";

    /// <inheritdoc />
    public string Name => GroupName;

    /// <inheritdoc />
    public string Prefix => string.Empty;

    /// <inheritdoc />
    public void ConfigureRoutes(RouteGroupBuilder group) =>
        group.AddEndpointFilter<RouteGroupBuilder, OpenIdEnvironmentEndpointFilter>();

    /// <inheritdoc />
    public void ConfigureServices(IEndpointGroupBuilder builder) =>
        builder.AddEndpoint<InteractiveAuthenticationEndpointHandler>();
}
