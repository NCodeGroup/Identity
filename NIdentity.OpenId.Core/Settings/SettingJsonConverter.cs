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
using System.Text.Json.Serialization;

namespace NIdentity.OpenId.Settings;

/// <summary>
/// Provides a <see cref="JsonConverter{T}"/> implementation that can serialize <see cref="Setting"/> instances as JSON.
/// </summary>
public class SettingJsonConverter : JsonConverter<Setting>
{
    private const string KeyPropertyName = "key";
    private const string ValuePropertyName = "value";

    private ISettingDescriptorProviderWrapper SettingDescriptorProviderWrapper { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingJsonConverter"/> class.
    /// </summary>
    /// <param name="settingDescriptorProviderWrapper">The <see cref="ISettingDescriptorProviderWrapper"/> instance.</param>
    public SettingJsonConverter(ISettingDescriptorProviderWrapper settingDescriptorProviderWrapper)
    {
        SettingDescriptorProviderWrapper = settingDescriptorProviderWrapper;
    }

    private readonly struct KeyEnvelope
    {
        public required string SettingName { get; init; }
        public required string ValueTypeName { get; init; }
    }

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeof(Setting).IsAssignableFrom(typeToConvert);

    /// <inheritdoc />
    public override Setting Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        if (!reader.Read())
            throw new JsonException();

        if (reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException();

        var keyProp = reader.GetString();
        if (keyProp != KeyPropertyName)
            throw new JsonException();

        if (!reader.Read())
            throw new JsonException();

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        var keyEnvelope = JsonSerializer.Deserialize<KeyEnvelope>(ref reader, options);

        if (reader.TokenType != JsonTokenType.EndObject)
            throw new JsonException();

        if (!reader.Read())
            throw new JsonException();

        if (reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException();

        var valueProp = reader.GetString();
        if (valueProp != ValuePropertyName)
            throw new JsonException();

        if (!reader.Read())
            throw new JsonException();

        var descriptor = SettingDescriptorProviderWrapper.GetDescriptor(keyEnvelope.SettingName, keyEnvelope.ValueTypeName);
        var value = JsonSerializer.Deserialize(ref reader, descriptor.ValueType, options);
        if (value == null)
            throw new JsonException();

        if (!reader.Read())
            throw new JsonException();

        if (reader.TokenType != JsonTokenType.EndObject)
            throw new JsonException();

        return descriptor.Create(value);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Setting setting, JsonSerializerOptions options)
    {
        var descriptor = setting.BaseDescriptor;

        var value = setting.GetValue();
        var valueTypeName = descriptor.ValueType.AssemblyQualifiedName ?? throw new InvalidOperationException();

        var keyEnvelope = new KeyEnvelope
        {
            SettingName = descriptor.SettingName,
            ValueTypeName = valueTypeName
        };

        writer.WriteStartObject();

        writer.WritePropertyName(KeyPropertyName);
        JsonSerializer.Serialize(writer, keyEnvelope, options);

        writer.WritePropertyName(ValuePropertyName);
        JsonSerializer.Serialize(writer, value, options);

        writer.WriteEndObject();
    }
}
