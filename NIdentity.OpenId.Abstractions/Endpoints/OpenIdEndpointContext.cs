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
using NIdentity.OpenId.Tenants;

namespace NIdentity.OpenId.Endpoints;

/// <summary>
/// Encapsulates all OpenID-specific information about an individual OpenID request.
/// </summary>
public abstract class OpenIdEndpointContext
{
    /// <summary>
    /// Gets the <see cref="HttpContext"/> associated with the current request.
    /// </summary>
    public abstract HttpContext HttpContext { get; }

    /// <summary>
    /// Gets the <see cref="OpenIdEndpointDescriptor"/> associated with the current request.
    /// </summary>
    public abstract OpenIdEndpointDescriptor EndpointDescriptor { get; }

    /// <summary>
    /// Gets the <see cref="OpenIdTenant"/> associated with the current request.
    /// </summary>
    public abstract OpenIdTenant Tenant { get; }
}
