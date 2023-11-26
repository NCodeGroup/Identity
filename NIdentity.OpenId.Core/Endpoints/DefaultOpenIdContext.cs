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
/// Provides a default implementation of the <see cref="OpenIdContext"/> abstraction.
/// </summary>
public class DefaultOpenIdContext(
    OpenIdServer openIdServer,
    OpenIdTenant openIdTenant,
    HttpContext httpContext,
    IMediator mediator,
    IPropertyBag propertyBag
) : OpenIdContext
{
    /// <inheritdoc />
    public override OpenIdServer OpenIdServer { get; } = openIdServer;

    /// <inheritdoc />
    public override OpenIdTenant OpenIdTenant { get; } = openIdTenant;

    /// <inheritdoc />
    public override HttpContext HttpContext { get; } = httpContext;

    /// <inheritdoc />
    public override IMediator Mediator { get; } = mediator;

    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; } = propertyBag;
}
