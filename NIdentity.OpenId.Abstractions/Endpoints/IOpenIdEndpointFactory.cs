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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace NIdentity.OpenId.Endpoints;

/// <summary>
/// Provides the ability to create <see cref="Endpoint"/> instances for <c>OAuth</c> or <c>OpenID Connect</c> routes.
/// </summary>
public interface IOpenIdEndpointFactory
{
    /// <summary>
    /// Creates a new <see cref="Endpoint"/> instance for the specified <c>OAuth</c> or <c>OpenID Connect</c> handler.
    /// </summary>
    /// <param name="name">The name for the endpoint.</param>
    /// <param name="path">The path for the endpoint.</param>
    /// <param name="httpMethods">The HTTP methods for the endpoint.</param>
    /// <param name="commandFactory">A delegate that is used to create the <see cref="OpenIdEndpointCommand"/> to dispatch requests for the endpoint.</param>
    /// <param name="configureRouteHandlerBuilder">A delegate to configure the <see cref="RouteHandlerBuilder"/> for the endpoint.</param>
    /// <returns>The newly created <see cref="Endpoint"/> for the <c>OAuth</c> or <c>OpenID Connect</c> handler.</returns>
    Endpoint CreateEndpoint(
        string name,
        PathString path,
        IEnumerable<string> httpMethods,
        OpenIdEndpointCommandFactory commandFactory,
        Action<RouteHandlerBuilder>? configureRouteHandlerBuilder = default);
}
