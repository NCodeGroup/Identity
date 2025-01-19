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
using Microsoft.AspNetCore.Routing;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Tenants;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Contexts;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdContext"/> abstraction.
/// </summary>
public class DefaultOpenIdContext(
    HttpContext httpContext,
    OpenIdEnvironment openIdEnvironment,
    OpenIdServer openIdServer,
    AsyncSharedReferenceLease<OpenIdTenant> tenantReference,
    IMediator mediator,
    IPropertyBag propertyBag
) : OpenIdContext
{
    private string? EndpointNameOrDefault { get; set; }

    private AsyncSharedReferenceLease<OpenIdTenant> TenantReference { get; set; } = tenantReference.AddReference();

    /// <inheritdoc />
    public override HttpContext Http { get; } = httpContext;

    /// <inheritdoc />
    public override OpenIdEnvironment Environment { get; } = openIdEnvironment;

    /// <inheritdoc />
    public override OpenIdServer Server { get; } = openIdServer;

    /// <inheritdoc />
    public override OpenIdTenant Tenant => TenantReference.Value;

    /// <inheritdoc />
    public override IMediator Mediator { get; } = mediator;

    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; } = propertyBag;

    /// <inheritdoc />
    public override string EndpointName => EndpointNameOrDefault ??= GetEndpointName();

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await TenantReference.DisposeAsync();
        TenantReference = default;
    }

    private string GetEndpointName() =>
        Http
            .GetEndpoint()?
            .Metadata
            .GetMetadata<IEndpointNameMetadata>()?
            .EndpointName ??
        string.Empty;
}
