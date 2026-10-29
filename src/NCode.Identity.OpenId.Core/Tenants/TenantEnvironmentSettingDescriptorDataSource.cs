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

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides an additive <see cref="SettingDescriptor"/> data source for the tenant environment settings:
/// <c>environment_type</c>, <c>environment_name</c> and <c>deletion_protection</c>. The domain of
/// <c>environment_type</c> is the registered <see cref="ITenantEnvironmentType"/> names, so a value that names no
/// registered type is rejected rather than stored. Each is a tenant setting that is not advertised in discovery.
/// </summary>
// See ADR-0289.
internal class TenantEnvironmentSettingDescriptorDataSource(
    INullChangeToken nullChangeToken,
    IEnumerable<ITenantEnvironmentType> types
) : ICollectionDataSource<SettingDescriptor>
{
    private const int MaxEnvironmentNameLength = 64;

    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    private ImmutableArray<string> TypeNames { get; } =
    [.. types.Select(type => type.TypeName).Order(StringComparer.Ordinal)];

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<SettingDescriptor> Collection
    {
        get
        {
            // environment_type: the name of the tenant's registered environment type, which selects that type's
            // security profiles. Production by default, so a tenant that never chose a type is treated as the most
            // protected one. Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.EnvironmentType,
                AppliesTo = [OpenIdSettingScopes.Tenant],
                Rule = SettingRules.OneOf(TypeNames),
                Default = OpenIdConstants.TenantEnvironmentTypes.Production,

                IsDiscoverable = false,
                OnMerge = SettingMerge.Replace,
            };

            // environment_name: a free-form display name for the tenant's environment, such as sandbox. Empty by
            // default, which shows no name. Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.EnvironmentName,
                AppliesTo = [OpenIdSettingScopes.Tenant],
                Rule = SettingRules.EmptyOr(
                    SettingRules.Must<string>(
                        $"at most {MaxEnvironmentNameLength} characters with no control characters",
                        value =>
                            value.Length <= MaxEnvironmentNameLength && !value.Any(char.IsControl)
                    )
                ),
                Default = string.Empty,

                IsDiscoverable = false,
                OnMerge = SettingMerge.Replace,
            };

            // deletion_protection: whether deleting the tenant is refused while the value is true. Off by default;
            // the production security profile turns it on. Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.DeletionProtection,
                AppliesTo = [OpenIdSettingScopes.Tenant],
                Default = false,

                IsDiscoverable = false,
                OnMerge = SettingMerge.Replace,
            };
        }
    }
}
