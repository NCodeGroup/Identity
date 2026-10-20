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

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Tenants.Strategies;

/// <summary>
/// Provides a tenant-selection strategy that resolves the tenant dynamically from a route parameter in the
/// request's path.
/// </summary>
internal class DynamicByPathTenantStrategy(
    IStoreManagerFactory storeManagerFactory,
    IOptions<TenantResolutionOptions> optionsAccessor,
    TemplateBinderFactory templateBinderFactory
) : TenantStrategy(storeManagerFactory, optionsAccessor)
{
    private DynamicByPathTenantOptions Options =>
        ResolutionOptions.DynamicByPath ?? new DynamicByPathTenantOptions();

    private TemplateBinder? TenantRouteBinderOrNull { get; set; }

    private TemplateBinder TenantRouteBinder =>
        TenantRouteBinderOrNull ??= templateBinderFactory.Create(GetTenantRoute());

    /// <inheritdoc />
    public override string StrategyCode => OpenIdConstants.TenantStrategyCodes.DynamicByPath;

    /// <inheritdoc />
    protected override PathString TenantPath => Options.TenantPath;

    /// <inheritdoc />
    protected override string? IssuerTemplate => Options.IssuerTemplate;

    /// <inheritdoc />
    /// <remarks>
    /// The tenant route carries only the tenant parameter, which is validated when the host starts, so binding it
    /// with the tenant's identifier yields the exact path the tenant is served under (for example <c>/acme</c>).
    /// </remarks>
    protected override string GetTenantRoutePath(string tenantId)
    {
        var values = new RouteValueDictionary { [Options.TenantIdRouteParameterName] = tenantId };
        return TenantRouteBinder.BindValues(values)
            ?? throw new InvalidOperationException(
                $"The tenant route '{TenantPath}' cannot be bound from the tenant identifier alone."
            );
    }

    /// <inheritdoc />
    public override bool TryGetTenantId(
        HttpContext httpContext,
        [NotNullWhen(true)] out string? tenantId
    )
    {
        if (
            httpContext.Request.RouteValues.TryGetValue(
                Options.TenantIdRouteParameterName,
                out var routeValue
            )
            && routeValue is string value
            && !string.IsNullOrEmpty(value)
        )
        {
            tenantId = value;
            return true;
        }

        tenantId = null;
        return false;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A request for the root tenant's route finds the root tenant even on a fresh deployment: it is created on demand.
    /// </remarks>
    public override async ValueTask<PersistedTenant> ResolveTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var tenantRoute = GetTenantRoute();

        if (tenantRoute.Parameters.Count == 0)
            throw new InvalidOperationException("The TenantRoute has no parameters.");

        if (!TryGetTenantId(httpContext, out var tenantId))
            throw new InvalidOperationException(
                $"The value for route parameter '{Options.TenantIdRouteParameterName}' is missing or empty."
            );

        return await GetTenantByIdAsync(httpContext, tenantId, cancellationToken);
    }
}
