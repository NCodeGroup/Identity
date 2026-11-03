// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides an additive <see cref="SettingDescriptor"/> data source for the limits on failed client authentications,
/// composed with the base descriptor catalog from the core slice: the failures one remote address may make for one
/// <c>client_id</c> and for every <c>client_id</c>, and the window they are counted in. Every setting applies to tenants,
/// has a default, so the limits hold unless a tenant changes them, and is not advertised.
/// </summary>
// See ADR-1399.
internal sealed class DefaultClientAuthenticationAttemptSettingDescriptorDataSource(
    INullChangeToken nullChangeToken
) : ICollectionDataSource<SettingDescriptor>
{
    private static ImmutableArray<string> Tenants { get; } = [OpenIdSettingScopes.Tenant];

    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<SettingDescriptor> Collection
    {
        get
        {
            // client_authentication_failed_attempts_limit: the failed client authentications one remote address may
            // make for one client_id within a window. Between 1 and 100; default 10, so one address can try at most 960
            // credentials of a client a day. Ceiling (Min): a narrower scope may only lower it.
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.ClientAuthenticationFailedAttemptsLimit,
                AppliesTo = Tenants,
                Rule = SettingRules.Between(1, 100),
                Default = 10,
                IsDiscoverable = false,
                OnMerge = SettingMerge.Min,
            };

            // client_authentication_failed_attempts_address_limit: the failed client authentications one remote address
            // may make for every client_id together within a window, which bounds an address that tries many
            // client_ids. Between 1 and 10,000; default 100. Ceiling (Min): a narrower scope may only lower it.
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.ClientAuthenticationFailedAttemptsAddressLimit,
                AppliesTo = Tenants,
                Rule = SettingRules.Between(1, 10_000),
                Default = 100,
                IsDiscoverable = false,
                OnMerge = SettingMerge.Min,
            };

            // client_authentication_failed_attempts_window: the length of the fixed windows the failures are counted
            // in, each starting at a multiple of its length. Between one minute and one day; default 15 minutes. Floor
            // (Max): a narrower scope may only lengthen it.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.ClientAuthenticationFailedAttemptsWindow,
                AppliesTo = Tenants,
                Rule = SettingRules.Between(TimeSpan.FromMinutes(1), TimeSpan.FromDays(1)),
                Default = TimeSpan.FromMinutes(15),
                IsDiscoverable = false,
                OnMerge = SettingMerge.Max,
            };
        }
    }
}
