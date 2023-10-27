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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using NIdentity.OpenId.Mediator;

namespace NIdentity.OpenId.Endpoints;

/// <summary>
/// Defines an <see cref="ICommand"/> contract that will dispatch <c>OAuth</c> or <c>OpenID Connect</c>
/// requests to the appropriate endpoint.
/// </summary>
/// <param name="HttpContext">The <see cref="HttpContext"/> instance.</param>
/// <param name="TenantRoute">The <see cref="RoutePattern"/> for the tenant's relative base path.</param>
/// <param name="Descriptor">The <see cref="OpenIdEndpointDescriptor"/> for the <c>OAuth</c> or <c>OpenID Connect</c> endpoint.</param>
/// <param name="CommandFactory">A delegate that is used to create <see cref="OpenIdEndpointCommand"/> instances which are used to dispatch requests for the endpoint.</param>
/// <param name="Mediator">The <see cref="IMediator"/> instance that is scoped to the current request.</param>
public record struct DispatchOpenIdEndpointCommand(
    HttpContext HttpContext,
    RoutePattern? TenantRoute,
    OpenIdEndpointDescriptor Descriptor,
    OpenIdEndpointCommandFactory CommandFactory,
    IMediator Mediator
) : ICommand;
