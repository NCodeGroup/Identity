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
/// Provides a <see cref="JsonConverter{T}"/> implementation that can serialize a collection of <see cref="Setting"/>
/// instances to and from JSON.
/// </summary>
public class SettingCollectionJsonConverter : JsonConverter<IEnumerable<Setting>>
{
    private IJsonSettingDescriptorCollection JsonSettingDescriptorCollection { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingCollectionJsonConverter"/> class.
    /// </summary>
    /// <param name="jsonSettingDescriptorCollection">The <see cref="IJsonSettingDescriptorCollection"/> instance.</param>
    public SettingCollectionJsonConverter(IJsonSettingDescriptorCollection jsonSettingDescriptorCollection)
    {
        JsonSettingDescriptorCollection = jsonSettingDescriptorCollection;
    }

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeof(IEnumerable<Setting>).IsAssignableFrom(typeToConvert);

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IEnumerable<Setting> settings, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var setting in settings)
        {
            writer.WritePropertyName(setting.Descriptor.Name);
            JsonSerializer.Serialize(writer, setting.GetValue(), setting.Descriptor.ValueType, options);
        }

        writer.WriteEndObject();
    }

    /// <inheritdoc />
    public override IEnumerable<Setting> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        var settings = new List<Setting>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            var name = reader.GetString();
            if (string.IsNullOrEmpty(name))
                throw new InvalidOperationException();

            if (!reader.Read())
                throw new JsonException();

            var descriptor = JsonSettingDescriptorCollection.GetDescriptor(name, reader.TokenType);
            var value = JsonSerializer.Deserialize(ref reader, descriptor.ValueType, options);
            if (value == null)
                throw new InvalidOperationException();

            var setting = descriptor.Create(value);
            settings.Add(setting);
        }

        if (reader.TokenType != JsonTokenType.EndObject)
            throw new JsonException();

        return settings;
    }
}
