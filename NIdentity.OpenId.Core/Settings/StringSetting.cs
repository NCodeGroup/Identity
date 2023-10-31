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
/// Provides an implementation of the <see cref="Setting{TValue}"/> abstraction for <see cref="string"/> values.
/// </summary>
public class StringSetting : Setting<string>
{
    /// <inheritdoc />
    public override SettingDescriptor Descriptor { get; }

    /// <inheritdoc />
    public override string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringSetting"/> class.
    /// </summary>
    /// <param name="descriptor">The <see cref="SettingDescriptor"/> instance.</param>
    /// <param name="value">The value for this setting.</param>
    public StringSetting(SettingDescriptor descriptor, string value)
    {
        if (descriptor.ValueType != typeof(string))
            throw new ArgumentException("Invalid ValueType.", nameof(descriptor));

        Descriptor = descriptor;
        Value = value;
    }

    /// <inheritdoc />
    public override Setting<string> Merge(Setting<string> other, SettingMergeOptions options = default)
    {
        var behavior = options.Behavior ?? Descriptor.DefaultMergeBehavior;
        return behavior switch
        {
            SettingMergeBehaviors.Scalar.Replace => this,
            SettingMergeBehaviors.Scalar.Append => new StringSetting(Descriptor, Value + other.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, JsonSerializerOptions options)
    {
        writer.WriteString(Descriptor.SettingName, Value);
    }
}
