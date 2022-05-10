#region Copyright Preamble

//
//    Copyright @ 2022 NCode Group
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
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Requests.Discovery;

/// <summary>
/// Defines a <see cref="OpenIdEndpointRequest"/> contract that accepts a <see cref="HttpContext"/> as an
/// input argument and expects <see cref="IHttpResult"/> as a response.
/// </summary>
/// <param name="HttpContext">The <see cref="HttpContext"/> input argument for the request contract.</param>
public record DiscoveryEndpointRequest(HttpContext HttpContext) : OpenIdEndpointRequest(HttpContext);
