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

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace NCode.Identity.OpenId.Endpoints;

/// <summary>
/// Provides the ability to map <c>OAuth</c> or <c>OpenID Connect</c> endpoints to the HTTP request pipeline.
/// </summary>
public static class OpenIdEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps all the <c>OAuth</c> or <c>OpenID Connect</c> endpoints that have been registered with the service provider.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> instance to map the endpoints to.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> instance that contains all the <c>OAuth</c> or <c>OpenID Connect</c> endpoints.</returns>
    public static RouteGroupBuilder MapOpenId(this IEndpointRouteBuilder endpoints) =>
        endpoints
            .ServiceProvider
            .GetRequiredService<IOpenIdEndpointRouteBuilder>()
            .MapOpenId(endpoints);
}
