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

using Microsoft.Extensions.Configuration;
using NIdentity.OpenId.Settings;

namespace NIdentity.OpenId.Servers;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdServerSettingsProvider"/> abstraction.
/// </summary>
public class OpenIdServerSettingsProvider : IOpenIdServerSettingsProvider
{
    private IConfiguration Configuration { get; }
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; }
    private ISettingCollection? SettingsOrNull { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdServerSettingsProvider"/> class.
    /// </summary>
    public OpenIdServerSettingsProvider(IConfiguration configuration, ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider)
    {
        Configuration = configuration;
        SettingDescriptorCollectionProvider = settingDescriptorCollectionProvider;
    }

    /// <inheritdoc />
    public ISettingCollection Settings => SettingsOrNull ??= LoadFromConfiguration(Configuration.GetSection("settings"));

    private SettingCollection LoadFromConfiguration(IConfiguration settingsSection)
    {
        var settings = new SettingCollection();
        var descriptors = SettingDescriptorCollectionProvider.Descriptors;

        foreach (var settingSection in settingsSection.GetChildren())
        {
            var settingName = settingSection.Key;
            if (!descriptors.TryGet(settingName, out var descriptor))
            {
                if (settingSection.GetChildren().Count() > 1)
                {
                    descriptor = new SettingDescriptor<IReadOnlyCollection<string>>
                    {
                        Name = settingName,
                        OnMerge = KnownSettings.Replace
                    };
                }
                else
                {
                    descriptor = new SettingDescriptor<string>
                    {
                        Name = settingName,
                        OnMerge = KnownSettings.Replace
                    };
                }
            }

            var value = settingSection.Get(descriptor.ValueType);
            if (value is null) continue;

            var setting = descriptor.Create(value);
            settings.Set(setting);
        }

        return settings;
    }
}
