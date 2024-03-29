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
using NCode.Identity;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Servers;
using NIdentity.OpenId.Tenants;

namespace NIdentity.OpenId.Endpoints;

/// <summary>
/// Encapsulates all OpenID-specific information about an individual OpenID request.
/// </summary>
public abstract class OpenIdContext
{
    /// <summary>
    /// Gets the <see cref="HttpContext"/> associated with the current request.
    /// </summary>
    public abstract HttpContext Http { get; }

    /// <summary>
    /// Gets the <see cref="OpenIdServer"/> associated with the current request.
    /// </summary>
    public abstract OpenIdServer Server { get; }

    /// <summary>
    /// Gets the <see cref="OpenIdTenant"/> associated with the current request.
    /// </summary>
    public abstract OpenIdTenant Tenant { get; }

    /// <summary>
    /// Gets the <see cref="IMediator"/> instance that is scoped with the current request.
    /// </summary>
    public abstract IMediator Mediator { get; }

    /// <summary>
    /// Gets the <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.
    /// </summary>
    public abstract IPropertyBag PropertyBag { get; }
}
