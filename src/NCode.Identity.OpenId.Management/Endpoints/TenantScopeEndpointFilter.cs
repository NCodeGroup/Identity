#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using Microsoft.Extensions.Options;
using NCode.Identity.Exceptions;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Tenants;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// A reusable endpoint filter that establishes the ambient tenant scope for tenant-bound endpoint families, so that
/// tenant-scoped data access can never materialize another tenant's resources (defense against cross-tenant data
/// leaks). For static-single (central-admin) deployments the filter is a no-op.
/// </summary>
internal sealed class TenantScopeEndpointFilter(
    IOptions<TenantResolutionOptions> optionsAccessor,
    ITenantResolver tenantResolver,
    IAmbientTenantAccessor ambientTenantAccessor
) : IEndpointFilter
{
    private TenantResolutionOptions Options { get; } = optionsAccessor.Value;

    private ITenantResolver TenantResolver { get; } = tenantResolver;

    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        // Static-single tenancy is a central-admin surface with no request-derived tenant boundary.
        if (
            string.Equals(
                Options.StrategyCode,
                OpenIdConstants.TenantStrategyCodes.StaticSingle,
                StringComparison.Ordinal
            )
        )
            return await next(context);

        var httpContext = context.HttpContext;

        PersistedTenant ambientTenant;
        try
        {
            ambientTenant = await TenantResolver.ResolveTenantAsync(
                httpContext,
                httpContext.RequestAborted
            );
        }
        catch (HttpResultException exception)
        {
            // An unresolvable ambient tenant (unknown host/path) is reported as its mapped result (typically 404).
            return exception.HttpResult;
        }

        using var scope = AmbientTenantAccessor.BeginScope(ambientTenant.TenantId);
        return await next(context);
    }
}
