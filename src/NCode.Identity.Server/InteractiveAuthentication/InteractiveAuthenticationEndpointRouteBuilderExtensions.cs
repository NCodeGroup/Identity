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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.Server.InteractiveAuthentication;

/// <summary>
/// Provides turnkey endpoint mapping for the interactive login/consent surface. The endpoints run inside the shared
/// OpenID request environment (so the resolved tenant scopes the local-account lookup), mirroring the protocol
/// endpoints' pipeline.
/// </summary>
[PublicAPI]
public static class InteractiveAuthenticationEndpointRouteBuilderExtensions
{
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps the interactive login/consent endpoints under
        /// <see cref="InteractiveAuthenticationDefaults.BasePath"/>: the login and consent pages (<c>GET</c>), the
        /// credential and consent submissions (<c>POST</c>), and logout (<c>GET</c>). The group materializes the
        /// OpenID request environment via <see cref="OpenIdEnvironmentEndpointFilter"/> and opens the tenant scope the
        /// local-account store reads. Requires <c>AddInteractiveAuthentication</c>.
        /// </summary>
        /// <returns>The same <see cref="IEndpointRouteBuilder"/> instance for method chaining.</returns>
        public IEndpointRouteBuilder MapInteractiveAuthentication()
        {
            var handler =
                endpoints.ServiceProvider.GetRequiredService<InteractiveAuthenticationEndpointHandler>();

            var group = endpoints
                .MapGroup(InteractiveAuthenticationDefaults.BasePath)
                .AddEndpointFilter<RouteGroupBuilder, OpenIdEnvironmentEndpointFilter>();

            group.MapGet("/login", handler.GetLoginPageAsync);
            group.MapPost("/login", handler.PostLoginAsync);
            group.MapGet("/consent", handler.GetConsentPageAsync);
            group.MapPost("/consent", InteractiveAuthenticationEndpointHandler.PostConsentAsync);
            group.MapGet("/logout", InteractiveAuthenticationEndpointHandler.LogoutAsync);

            return endpoints;
        }
    }
}
