// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.Usage;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Provides an additive <see cref="RateLimitDescriptor"/> data source for the server's own rate limits and daily
/// quotas: the per-minute limits for a remote address, a tenant and an authenticated client, the daily request quota,
/// and the daily token-request quota. Each reads its limit and overage from the settings that the rate-limit setting
/// descriptors register.
/// </summary>
// See ADR-1246.
internal class DefaultRateLimitDescriptorDataSource(INullChangeToken nullChangeToken)
    : ICollectionDataSource<RateLimitDescriptor>
{
    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<RateLimitDescriptor> Collection
    {
        get
        {
            // address, order 100: the narrowest per-minute limit is judged first, and a request it refuses is not
            // counted against the tenant, so one address over its own limit cannot spend the allowance every other
            // caller of the tenant shares. A request without a remote address is not limited by address.
            yield return new RateLimitDescriptor
            {
                Name = RateLimitPartitions.Address,
                Stage = RateLimitStages.Request,
                Order = 100,
                Window = TimeSpan.FromMinutes(1),
                IsSliding = true,
                LimitKey = OpenIdSettingKeys.RateLimitAddressPerMinute,
                PartitionValueSelector = static context =>
                    context.OpenIdContext.Http.Connection.RemoteIpAddress is { } address
                        ? RemoteAddressPartition.GetValue(address)
                        : null,
            };

            // tenant, order 200: the per-minute limit across every caller, judged after the address limit and before
            // the daily quota, so a request that only the tenant limit refuses does not spend the quota.
            yield return new RateLimitDescriptor
            {
                Name = RateLimitPartitions.Tenant,
                Stage = RateLimitStages.Request,
                Order = 200,
                Window = TimeSpan.FromMinutes(1),
                IsSliding = true,
                LimitKey = OpenIdSettingKeys.RateLimitTenantPerMinute,
                PartitionValueSelector = static _ => string.Empty,
            };

            // daily_requests, order 300: the daily quota is judged last, so a request a per-minute limit refuses does
            // not spend it. Only a metered request spends it; public metadata such as discovery and the key set never
            // does. A tier that sets an overage admits that many more requests past the quota as billable overage.
            yield return new RateLimitDescriptor
            {
                Name = RateLimitPartitions.DailyRequests,
                Stage = RateLimitStages.Request,
                Order = 300,
                Window = TimeSpan.FromDays(1),
                LimitKey = OpenIdSettingKeys.QuotaTenantRequestsPerDay,
                OverageKey = OpenIdSettingKeys.QuotaTenantRequestsOveragePerDay,
                IsMeteredOnly = true,
                UsageMeter = TenantUsageMeters.Requests,
                OverageUsageMeter = TenantUsageMeters.RequestsOverage,
                PartitionValueSelector = static _ => string.Empty,
            };

            // client, order 100: the only limit of the client-authentication stage, which a confidential client
            // reaches once it has authenticated. A public client has no context client here, so it is not limited.
            yield return new RateLimitDescriptor
            {
                Name = RateLimitPartitions.Client,
                Stage = RateLimitStages.ClientAuthentication,
                Order = 100,
                Window = TimeSpan.FromMinutes(1),
                IsSliding = true,
                LimitKey = OpenIdSettingKeys.RateLimitClientPerMinute,
                PartitionValueSelector = static context => context.Client?.ClientId,
            };

            // daily_token_requests, order 100: the only limit of the token-request stage, so a token request that the
            // client limit refused has not spent the token-request quota.
            yield return new RateLimitDescriptor
            {
                Name = RateLimitPartitions.DailyTokenRequests,
                Stage = RateLimitStages.TokenRequest,
                Order = 100,
                Window = TimeSpan.FromDays(1),
                LimitKey = OpenIdSettingKeys.QuotaTenantTokenRequestsPerDay,
                OverageKey = OpenIdSettingKeys.QuotaTenantTokenRequestsOveragePerDay,
                UsageMeter = TenantUsageMeters.TokenRequests,
                OverageUsageMeter = TenantUsageMeters.TokenRequestsOverage,
                PartitionValueSelector = static _ => string.Empty,
            };
        }
    }
}
