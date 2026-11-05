// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Provides an additive <see cref="SettingDescriptor"/> data source for the team settings: <c>max_tenants</c>, the
/// most tenants a team may hold. It is a team setting that is not advertised in discovery.
/// </summary>
// See ADR-1411.
// See ADR-1418.
internal class TeamSettingDescriptorDataSource(INullChangeToken nullChangeToken)
    : ICollectionDataSource<SettingDescriptor>
{
    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<SettingDescriptor> Collection
    {
        get
        {
            // max_tenants: the most tenants a team may hold. Unset means unlimited. A limit: the platform's
            // administrators set it on a team, and Min keeps the platform's ceiling.
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.MaxTenants,
                AppliesTo = [OpenIdSettingScopes.Team],
                Rule = SettingRules.AtLeast(0),

                IsDiscoverable = false,
                IsLimit = true,
                OnMerge = SettingMerge.Min,
            };
        }
    }
}
