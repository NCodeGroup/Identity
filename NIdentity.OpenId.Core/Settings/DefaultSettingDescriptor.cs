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
/// Provides a default implementation of the <see cref="SettingDescriptor"/> abstraction.
/// </summary>
public class DefaultSettingDescriptor : SettingDescriptor
{
    /// <inheritdoc />
    public override Type ValueType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSettingDescriptor"/> class.
    /// </summary>
    /// <param name="valueType">The type of the setting value.</param>
    public DefaultSettingDescriptor(Type valueType)
    {
        ValueType = valueType;
    }

    /// <inheritdoc />
    public override Setting Create(object value)
    {
        if (!ValueType.IsInstanceOfType(value))
            throw new ArgumentException("Incompatible ValueType.", nameof(value));

        var settingType = typeof(Setting<>).MakeGenericType(ValueType);
        return (Setting)Activator.CreateInstance(settingType, this, value)!;
    }

    /// <inheritdoc />
    public override Setting Merge(Setting current, Setting other)
    {
        // TODO: use reflection to check for IEnumerable<> value types
        return other;
    }
}
