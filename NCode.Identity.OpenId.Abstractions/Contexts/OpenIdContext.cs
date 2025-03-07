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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Tenants;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Contexts;

/// <summary>
/// Encapsulates all OpenID-specific information about an individual OpenID request.
/// </summary>
[PublicAPI]
public abstract class OpenIdContext : IAsyncDisposable
{
    /// <summary>
    /// Gets the <see cref="HttpContext"/> associated with the current request.
    /// </summary>
    public abstract HttpContext Http { get; }

    /// <summary>
    /// Gets the <see cref="OpenIdEnvironment"/> associated with the current request.
    /// </summary>
    public abstract OpenIdEnvironment Environment { get; }

    /// <summary>
    /// Gets the <see cref="IOpenIdErrorFactory"/> instance that can be used to create error responses
    /// </summary>
    public virtual IOpenIdErrorFactory ErrorFactory => Environment.ErrorFactory;

    /// <summary>
    /// Gets the <see cref="OpenIdServer"/> associated with the current request.
    /// </summary>
    public abstract OpenIdServer Server { get; }

    /// <summary>
    /// Gets the <see cref="OpenIdTenant"/> associated with the current request.
    /// </summary>
    public abstract OpenIdTenant Tenant { get; }

    /// <summary>
    /// Gets the <see cref="IMediator"/> instance that is scoped to the current request.
    /// </summary>
    public abstract IMediator Mediator { get; }

    /// <summary>
    /// Gets the <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.
    /// </summary>
    public abstract IPropertyBag PropertyBag { get; }

    /// <summary>
    /// Gets the name of the endpoint associated with the current request.
    /// </summary>
    public abstract string EndpointName { get; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    protected abstract ValueTask DisposeAsyncCore();
}
