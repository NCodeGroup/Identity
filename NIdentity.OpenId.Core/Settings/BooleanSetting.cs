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

using System.Text.Json;

namespace NIdentity.OpenId.Settings;

/// <summary>
/// Provides an implementation of the <see cref="Setting{TValue}"/> abstraction for <see cref="bool"/> values.
/// </summary>
public class BooleanSetting : Setting<bool>
{
    /// <inheritdoc />
    public override SettingDescriptor Descriptor { get; }

    /// <inheritdoc />
    public override bool Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BooleanSetting"/> class.
    /// </summary>
    /// <param name="descriptor">The <see cref="SettingDescriptor"/> instance.</param>
    /// <param name="value">The value for this setting.</param>
    public BooleanSetting(SettingDescriptor descriptor, bool value)
    {
        if (descriptor.ValueType != typeof(bool))
            throw new ArgumentException("Invalid ValueType.", nameof(descriptor));

        Descriptor = descriptor;
        Value = value;
    }

    /// <inheritdoc />
    public override Setting<bool> Merge(Setting<bool> other, SettingMergeOptions options = default)
    {
        var behavior = options.Behavior ?? Descriptor.DefaultMergeBehavior;
        var newValue = behavior switch
        {
            SettingMergeBehaviors.Boolean.And => Value && other.Value,
            SettingMergeBehaviors.Boolean.Or => Value || other.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };
        return new BooleanSetting(Descriptor, newValue);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, JsonSerializerOptions options)
    {
        writer.WriteBoolean(Descriptor.SettingName, Value);
    }
}
