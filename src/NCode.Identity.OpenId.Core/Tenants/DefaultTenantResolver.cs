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

using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Options;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides a default implementation of the <see cref="ITenantResolver"/> abstraction that selects the configured
/// <see cref="ITenantResolverStrategy"/> by <see cref="TenantResolutionOptions.ProviderCode"/> and delegates to it.
/// </summary>
internal class DefaultTenantResolver(
    IOptions<TenantResolutionOptions> optionsAccessor,
    IEnumerable<ITenantResolverStrategy> strategies
) : ITenantResolver
{
    private TenantResolutionOptions Options { get; } = optionsAccessor.Value;

    private ImmutableArray<ITenantResolverStrategy> Strategies { get; } = [.. strategies];

    private ITenantResolverStrategy? SelectedOrNull { get; set; }

    private ITenantResolverStrategy Selected => SelectedOrNull ??= SelectStrategy();

    private ITenantResolverStrategy SelectStrategy()
    {
        var providerCode = Options.ProviderCode;
        return Strategies.FirstOrDefault(strategy =>
                string.Equals(providerCode, strategy.ProviderCode, StringComparison.Ordinal)
            )
            ?? throw new InvalidOperationException(
                $"Unable to find a tenant resolver strategy with code '{providerCode}'."
            );
    }

    /// <inheritdoc />
    public RoutePattern GetTenantRoute(IPropertyBag propertyBag) =>
        Selected.GetTenantRoute(propertyBag);

    /// <inheritdoc />
    public async ValueTask<TenantDescriptor?> ResolveDescriptorAsync(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    ) => await Selected.ResolveDescriptorAsync(httpContext, propertyBag, cancellationToken);
}
