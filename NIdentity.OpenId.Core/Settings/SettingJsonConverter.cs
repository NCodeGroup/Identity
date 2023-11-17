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
    private const string NameProperty = "name";
    private const string ValueProperty = "value";

    private IJsonSettingDescriptorCollection JsonSettingDescriptorCollection { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingJsonConverter"/> class.
    /// </summary>
    /// <param name="jsonSettingDescriptorCollection">The <see cref="IJsonSettingDescriptorCollection"/> instance.</param>
    public SettingJsonConverter(IJsonSettingDescriptorCollection jsonSettingDescriptorCollection)
    {
        JsonSettingDescriptorCollection = jsonSettingDescriptorCollection;
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

        var settingName = ReadSettingName(ref reader);

        if (!reader.Read())
            throw new JsonException();

        var (descriptor, value) = ReadSettingValue(ref reader, options, settingName);

        if (!reader.Read())
            throw new JsonException();

        if (reader.TokenType != JsonTokenType.EndObject)
            throw new JsonException();

        return descriptor.Create(value);
    }

    private static string ReadSettingName(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException();

        var nameProp = reader.GetString();
        if (nameProp != NameProperty)
            throw new JsonException();

        if (!reader.Read())
            throw new JsonException();

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException();

        var settingName = reader.GetString();
        if (string.IsNullOrEmpty(settingName))
            throw new InvalidOperationException();

        return settingName;
    }

    private (SettingDescriptor Descriptor, object Value) ReadSettingValue(ref Utf8JsonReader reader, JsonSerializerOptions options, string settingName)
    {
        if (reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException();

        var valueProp = reader.GetString();
        if (valueProp != ValueProperty)
            throw new JsonException();

        if (!reader.Read())
            throw new JsonException();

        var descriptor = JsonSettingDescriptorCollection.GetDescriptor(settingName, reader.TokenType);
        var value = JsonSerializer.Deserialize(ref reader, descriptor.ValueType, options);
        if (value == null)
            throw new InvalidOperationException();

        return (descriptor, value);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Setting setting, JsonSerializerOptions options)
    {
        var settingName = setting.Descriptor.Name;
        var settingValue = setting.GetValue();

        writer.WriteStartObject();

        writer.WriteString(NameProperty, settingName);

        writer.WritePropertyName(ValueProperty);
        JsonSerializer.Serialize(writer, settingValue, options);

        writer.WriteEndObject();
    }
}
