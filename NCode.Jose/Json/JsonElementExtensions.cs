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

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NCode.Jose.Json;

public static class JsonElementExtensions
{
    public static bool TryGetPropertyValue<T>(this JsonObject jsonObject, string propertyName, [MaybeNullWhen(false)] out T value)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var property) || property == null)
        {
            value = default;
            return false;
        }

        return property.AsValue().TryGetValue(out value);
    }

    public static bool TryGetPropertyValue<T>(this JsonElement jsonElement, string propertyName, [MaybeNullWhen(false)] out T value)
    {
        if (!jsonElement.TryGetProperty(propertyName, out var property))
        {
            value = default;
            return false;
        }

        if (typeof(T) == typeof(JsonElement))
        {
            value = (T)(object)property;
            return true;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            value = default;
            return false;
        }

        if (typeof(T) == typeof(string))
        {
            value = (T)(object)property.ToString();
            return true;
        }

        if (typeof(bool).IsAssignableTo(typeof(T)) && property.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = (T)(object)property.GetBoolean();
            return true;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            if (typeof(float).IsAssignableTo(typeof(T)) && property.TryGetSingle(out var floatValue))
            {
                value = (T)(object)floatValue;
                return true;
            }

            if (typeof(double).IsAssignableTo(typeof(T)) && property.TryGetDouble(out var doubleValue))
            {
                value = (T)(object)doubleValue;
                return true;
            }

            if (typeof(short).IsAssignableTo(typeof(T)) && property.TryGetInt16(out var shortValue))
            {
                value = (T)(object)shortValue;
                return true;
            }

            if (typeof(int).IsAssignableTo(typeof(T)) && property.TryGetInt32(out var intValue))
            {
                value = (T)(object)intValue;
                return true;
            }

            if (typeof(long).IsAssignableTo(typeof(T)) && property.TryGetInt64(out var longValue))
            {
                value = (T)(object)longValue;
                return true;
            }
        }

        Debug.Fail("Not Implemented");
        value = default;
        return false;
    }
}
