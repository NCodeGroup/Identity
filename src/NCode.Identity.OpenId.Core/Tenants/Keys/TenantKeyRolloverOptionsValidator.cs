// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Identity.Time;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Validates the options of the tenant key rollover when the host starts: the pass interval is one a timer accepts,
/// and every token lifetime setting names a registered setting whose value is a duration and that a tenant's
/// effective settings read, so the retirement grace covers the lifetime it names rather than silently ignoring a
/// name that matches no setting.
/// </summary>
// See ADR-1137 (configuration is validated at startup), ADR-1149 (a setting applies within the scopes its own
// cascade into), ADR-1137 and ADR-1404.
internal sealed class TenantKeyRolloverOptionsValidator(
    ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider,
    ISettingScopeCollectionProvider settingScopeCollectionProvider
) : IValidateOptions<OpenIdOptions>
{
    private static string Path(string member) =>
        $"{nameof(OpenIdOptions.Tenant)}:{nameof(OpenIdTenantOptions.KeyRollover)}:{member}";

    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; } =
        settingDescriptorCollectionProvider;
    private ISettingScopeCollectionProvider SettingScopeCollectionProvider { get; } =
        settingScopeCollectionProvider;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OpenIdOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var rollover = options.Tenant.KeyRollover;
        List<string> failures = [];

        if (!TimerPeriod.IsValid(rollover.Interval))
        {
            failures.Add(
                $"{Path(nameof(OpenIdTenantKeyRolloverOptions.Interval))} must be between {TimerPeriod.MinValue} and {TimerPeriod.MaxValue}, the periods a timer accepts; it is {rollover.Interval}."
            );
        }

        var tokenLifetimeSettingsPath = Path(
            nameof(OpenIdTenantKeyRolloverOptions.TokenLifetimeSettings)
        );
        var descriptors = SettingDescriptorCollectionProvider.Collection;
        var scopes = SettingScopeCollectionProvider.Collection;
        foreach (var settingName in rollover.TokenLifetimeSettings)
        {
            if (string.IsNullOrWhiteSpace(settingName))
            {
                failures.Add($"{tokenLifetimeSettingsPath} must name a setting in every entry.");
            }
            else if (
                !descriptors.TryGet(settingName, out var descriptor)
                || descriptor.ValueType != typeof(TimeSpan)
                || !scopes.AppliesWithin(descriptor, OpenIdSettingScopes.Tenant)
            )
            {
                failures.Add(
                    $"{tokenLifetimeSettingsPath} names '{settingName}', which is not a registered setting that a tenant's effective settings read as a duration."
                );
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
