// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A test <see cref="IDefaultSettingsProvider"/> that widens the platform ceiling by unioning the
/// <c>client_credentials</c> grant type and a custom <c>api</c> scope on top of the library baseline.
/// Demonstrates that an extension can contribute capabilities the library does not bake in.
/// </summary>
internal sealed class TestPlatformSettingsProvider : IDefaultSettingsProvider
{
    public const string ApiScope = "api";

    public void Configure(ISettingCollection settings)
    {
        Union(
            settings,
            OpenIdSettingKeys.GrantTypesSupported,
            OpenIdProtocol.GrantTypes.ClientCredentials
        );
        Union(settings, OpenIdSettingKeys.GrantTypesSupported, OpenIdProtocol.GrantTypes.Password);
    }

    private static void Union(
        ISettingCollection settings,
        SettingKey<IReadOnlyCollection<string>> key,
        string value
    )
    {
        IReadOnlyCollection<string> current = settings.TryGetValue(key, out var existing)
            ? existing
            : [];

        if (!current.Contains(value))
        {
            settings.Set(key, [.. current, value]);
        }
    }
}
