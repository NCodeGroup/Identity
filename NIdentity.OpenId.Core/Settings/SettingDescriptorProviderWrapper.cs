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

namespace NIdentity.OpenId.Settings;

/// <summary>
/// Provides a wrapper for the <see cref="ISettingDescriptorProvider"/> abstraction.
/// </summary>
public interface ISettingDescriptorProviderWrapper
{
    /// <summary>
    /// Gets a strongly typed <see cref="SettingDescriptor"/> with the specified key and value type name.
    /// </summary>
    /// <param name="settingName">The name of the setting.</param>
    /// <param name="valueTypeName">The value type name of the setting.</param>
    /// <returns>The <see cref="SettingDescriptor"/> instance.</returns>
    SettingDescriptor GetDescriptor(string settingName, string valueTypeName);
}

/// <summary>
/// Provides a default implementation of the <see cref="ISettingDescriptorProviderWrapper"/> abstraction.
/// </summary>
public class SettingDescriptorProviderWrapper : ISettingDescriptorProviderWrapper
{
    private ISettingDescriptorProvider SettingDescriptorProvider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingDescriptorProviderWrapper"/> class.
    /// </summary>
    /// <param name="settingDescriptorProvider">The <see cref="ISettingDescriptorProvider"/> instance.</param>
    public SettingDescriptorProviderWrapper(ISettingDescriptorProvider settingDescriptorProvider)
    {
        SettingDescriptorProvider = settingDescriptorProvider;
    }

    /// <inheritdoc />
    public SettingDescriptor GetDescriptor(string settingName, string valueTypeName)
    {
        var valueType = Type.GetType(valueTypeName);
        if (valueType == null)
            throw new InvalidOperationException();

        var settingKey = new SettingKey
        {
            SettingName = settingName,
            ValueType = valueType
        };

        if (!SettingDescriptorProvider.TryGet(settingKey, out var descriptor))
        {
            descriptor = new DefaultSettingDescriptor(valueType)
            {
                SettingName = settingName
            };
        }

        return descriptor;
    }
}
