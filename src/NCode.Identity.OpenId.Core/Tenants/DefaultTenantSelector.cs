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
using NCode.Identity.OpenId.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides a default implementation of the <see cref="ITenantSelector"/> abstraction that selects the configured
/// <see cref="ITenantStrategy"/> by <see cref="TenantResolutionOptions.StrategyCode"/> and delegates to it.
/// </summary>
internal class DefaultTenantSelector(
    IOptions<TenantResolutionOptions> optionsAccessor,
    IEnumerable<ITenantStrategy> strategies
) : ITenantSelector
{
    private TenantResolutionOptions Options { get; } = optionsAccessor.Value;

    private ImmutableArray<ITenantStrategy> Strategies { get; } = [.. strategies];

    private ITenantStrategy? SelectedOrNull { get; set; }

    private ITenantStrategy Selected => SelectedOrNull ??= SelectStrategy();

    private ITenantStrategy SelectStrategy()
    {
        var strategyCode = Options.StrategyCode;
        return Strategies.FirstOrDefault(strategy =>
                string.Equals(strategyCode, strategy.StrategyCode, StringComparison.Ordinal)
            )
            ?? throw new InvalidOperationException(
                $"Unable to find a tenant strategy with code '{strategyCode}'."
            );
    }

    /// <inheritdoc />
    public RoutePattern GetTenantRoute() => Selected.GetTenantRoute();

    /// <inheritdoc />
    public async ValueTask<PersistedTenant> ResolveTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    ) => await Selected.ResolveTenantAsync(httpContext, cancellationToken);
}
