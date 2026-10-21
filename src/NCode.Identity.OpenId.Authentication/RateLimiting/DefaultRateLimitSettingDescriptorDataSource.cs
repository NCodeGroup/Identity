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

using System.Collections.Immutable;
using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Provides an additive <see cref="SettingDescriptor"/> data source for the rate-limit settings, composed with the base
/// descriptor catalog from the core slice. Each limit counts requests per minute, has no default (unset means
/// unlimited), and merges as a ceiling: a narrower scope may lower its parent's limit but never raise it.
/// </summary>
// See ADR-0196.
internal class DefaultRateLimitSettingDescriptorDataSource(INullChangeToken nullChangeToken)
    : ICollectionDataSource<SettingDescriptor>
{
    private static ImmutableArray<string> Tenants { get; } = [OpenIdSettingScopes.Tenant];

    private static ImmutableArray<string> TenantsAndClients { get; } =
    [OpenIdSettingScopes.Tenant, OpenIdSettingScopes.Client];

    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<SettingDescriptor> Collection
    {
        get
        {
            // rate_limit_tenant_per_minute: the most requests per minute the tenant's protocol endpoints serve,
            // across every caller.
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.RateLimitTenantPerMinute,
                AppliesTo = Tenants,
                Rule = SettingRules.AtLeast(1),
                OnMerge = SettingMerge.Min,
            };

            // rate_limit_address_per_minute: the most requests per minute the tenant's protocol endpoints serve to one
            // remote address. A separate limit from the per-client one, because many clients may share an address
            // (a NAT gateway or a backend), and one client may call from many.
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.RateLimitAddressPerMinute,
                AppliesTo = Tenants,
                Rule = SettingRules.AtLeast(1),
                OnMerge = SettingMerge.Min,
            };

            // rate_limit_client_per_minute: the most requests per minute the tenant's protocol endpoints accept from
            // one authenticated client; set on the tenant as every client's ceiling, or on one client.
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.RateLimitClientPerMinute,
                AppliesTo = TenantsAndClients,
                Rule = SettingRules.AtLeast(1),
                OnMerge = SettingMerge.Min,
            };

            // quota_tenant_requests_per_day: the most requests the tenant's protocol endpoints serve in one UTC calendar
            // day; a plan tier writes it on the tenant, beneath the server's ceiling (ADR-0198).
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.QuotaTenantRequestsPerDay,
                AppliesTo = Tenants,
                Rule = SettingRules.AtLeast(1),
                OnMerge = SettingMerge.Min,
            };

            // quota_tenant_token_requests_per_day: the most token requests from confidential clients the tenant's token
            // endpoint serves in one UTC calendar day (ADR-0198).
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.QuotaTenantTokenRequestsPerDay,
                AppliesTo = Tenants,
                Rule = SettingRules.AtLeast(1),
                OnMerge = SettingMerge.Min,
            };

            // quota_tenant_requests_overage_per_day: the most requests admitted past the daily request quota in one UTC
            // calendar day, each billed as overage; unset refuses at the quota. Merged with Min, so the server's value
            // caps every tier's overage (ADR-0200).
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.QuotaTenantRequestsOveragePerDay,
                AppliesTo = Tenants,
                Rule = SettingRules.AtLeast(0),
                OnMerge = SettingMerge.Min,
            };

            // quota_tenant_token_requests_overage_per_day: the most confidential clients' token requests admitted past
            // the daily token-request quota in one UTC calendar day, each billed as overage (ADR-0200).
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.QuotaTenantTokenRequestsOveragePerDay,
                AppliesTo = Tenants,
                Rule = SettingRules.AtLeast(0),
                OnMerge = SettingMerge.Min,
            };
        }
    }
}
