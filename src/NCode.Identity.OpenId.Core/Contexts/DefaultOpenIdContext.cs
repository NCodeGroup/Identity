// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Tenants;
using NCode.Mediator;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Contexts;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdContext"/> abstraction.
/// </summary>
internal class DefaultOpenIdContext(
    HttpContext httpContext,
    OpenIdEnvironment openIdEnvironment,
    OpenIdPlatform openIdPlatform,
    AsyncSharedReferenceLease<OpenIdTenant> tenantReference,
    IMediator mediator,
    IPropertyBag propertyBag
) : OpenIdContext
{
    private string? EndpointNameOrDefault { get; set; }

    private AsyncSharedReferenceLease<OpenIdTenant> TenantReference { get; set; } =
        tenantReference.AddReference();

    /// <inheritdoc />
    public override HttpContext Http { get; } = httpContext;

    /// <inheritdoc />
    public override OpenIdEnvironment Environment { get; } = openIdEnvironment;

    /// <inheritdoc />
    public override OpenIdPlatform Platform { get; } = openIdPlatform;

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
        Http.GetEndpoint()?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
        ?? string.Empty;
}
