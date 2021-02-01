#region Copyright Preamble

// 
//    Copyright @ 2021 NCode Group
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

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NIdentity.OpenId.Core.Tests.Messages
{
    internal class TestNestedObjectJsonConverter : JsonConverter<ITestNestedObject?>
    {
        public override ITestNestedObject? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize<TestNestedObject>(ref reader, options);
        }

        public override void Write(Utf8JsonWriter writer, ITestNestedObject? value, JsonSerializerOptions options)
        {
            var type = value?.GetType() ?? typeof(TestNestedObject);
            JsonSerializer.Serialize(writer, value, type, options);
        }
    }
}
