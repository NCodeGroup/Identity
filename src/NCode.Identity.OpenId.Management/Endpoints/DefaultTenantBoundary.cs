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
using NCode.Identity.OpenId.Tenants;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Provides the default implementation of <see cref="ITenantBoundary"/> that reuses the shared
/// <see cref="ITenantResolver"/> to determine the request's tenant scope.
/// </summary>
internal class DefaultTenantBoundary(
    ITenantResolver tenantResolver,
    IOptions<TenantResolutionOptions> optionsAccessor
) : ITenantBoundary
{
    private ITenantResolver TenantResolver { get; } = tenantResolver;

    private TenantResolutionOptions Options { get; } = optionsAccessor.Value;

    /// <inheritdoc />
    public async ValueTask<ManagementError?> CheckAsync(
        HttpContext httpContext,
        string resourceTenantId,
        CancellationToken cancellationToken
    )
    {
        // Central-admin surface: static-single tenancy carries no request-derived boundary.
        if (
            string.Equals(
                Options.ProviderCode,
                OpenIdConstants.TenantProviderCodes.StaticSingle,
                StringComparison.Ordinal
            )
        )
            return null;

        var propertyBag = PropertyBagFactory.Create();
        var descriptor = await TenantResolver.ResolveDescriptorAsync(
            httpContext,
            propertyBag,
            cancellationToken
        );

        // No ambient tenant scope resolved for this request; treat as unscoped.
        if (descriptor is not { } scope)
            return null;

        if (string.Equals(scope.TenantId, resourceTenantId, StringComparison.Ordinal))
            return null;

        // Return 404 (not 403) so a scoped surface cannot probe the existence of another tenant's resources.
        return new ManagementError
        {
            StatusCode = StatusCodes.Status404NotFound,
            Detail = "The specified resource could not be found.",
        };
    }
}
