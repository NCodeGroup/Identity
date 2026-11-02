// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NCode.Identity.OpenId.Core.Tests.Messages;

internal class TestNestedObjectJsonConverter : JsonConverter<ITestNestedObject?>
{
    public override ITestNestedObject? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        return JsonSerializer.Deserialize<TestNestedObject>(ref reader, options);
    }

    public override void Write(
        Utf8JsonWriter writer,
        ITestNestedObject? value,
        JsonSerializerOptions options
    )
    {
        var type = value?.GetType() ?? typeof(TestNestedObject);
        JsonSerializer.Serialize(writer, value, type, options);
    }
}
