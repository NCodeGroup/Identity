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

using NCode.Identity.OpenId.Authentication.Settings;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.IntegrationTests.Infrastructure;

/// <summary>
/// A test <see cref="IDefaultSettingsProvider"/> that widens the server ceiling by unioning the
/// <c>client_credentials</c> grant type and a custom <c>api</c> scope on top of the library baseline.
/// Demonstrates that an extension can contribute capabilities the library does not bake in.
/// </summary>
internal sealed class TestServerSettingsProvider : IDefaultSettingsProvider
{
    public const string ApiScope = "api";

    public void Configure(ISettingCollection settings)
    {
        Union(
            settings,
            OpenIdSettingKeys.GrantTypesSupported,
            OpenIdConstants.GrantTypes.ClientCredentials
        );
        Union(settings, OpenIdSettingKeys.GrantTypesSupported, OpenIdConstants.GrantTypes.Password);
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
