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

using System.Reflection;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides a default implementation of the <see cref="ISettingDescriptorCollectionProvider"/> abstraction.
/// </summary>
public class SettingDescriptorCollectionProvider : ISettingDescriptorCollectionProvider
{
    /// <inheritdoc />
    public ISettingDescriptorCollection Descriptors { get; } = CreateCollection();

    // TODO: make this configurable

    private static SettingDescriptorCollection CreateCollection()
    {
        var descriptors = typeof(KnownSettings)
            .GetProperties(BindingFlags.Static | BindingFlags.Public)
            .Where(p => typeof(SettingDescriptor).IsAssignableFrom(p.PropertyType))
            .Select(p => (SettingDescriptor?)p.GetValue(null))
            .Where(d => d is not null)
            .Cast<SettingDescriptor>();

        return new SettingDescriptorCollection(descriptors);
    }
}
