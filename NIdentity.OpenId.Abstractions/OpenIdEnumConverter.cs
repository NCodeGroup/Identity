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

using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace NIdentity.OpenId;

/// <summary>
/// Implements a <see cref="TypeConverter"/> for converting between enum values and <see cref="string"/> values.
/// </summary>
/// <typeparam name="TEnum">The type of the enum.</typeparam>
public class OpenIdEnumConverter<TEnum> : EnumConverter
    where TEnum : struct, Enum
{
    private Dictionary<TEnum, string> LookupFromEnum { get; } = new();
    private Dictionary<string, TEnum> LookupFromString { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdEnumConverter{TEnum}"/> class.
    /// </summary>
    public OpenIdEnumConverter()
        : base(typeof(TEnum))
    {
        var fields = EnumType.GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (var field in fields)
        {
            var value = (TEnum)field.GetValue(null)!;
            var label = field.GetCustomAttribute<OpenIdLabelAttribute>()?.Label ?? field.Name;

            LookupFromEnum[value] = label;
            LookupFromString[label] = value;
        }
    }

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string stringValue && LookupFromString.TryGetValue(stringValue, out var enumValue))
            return enumValue;

        return base.ConvertFrom(context, culture, value);
    }

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is TEnum enumValue && LookupFromEnum.TryGetValue(enumValue, out var label))
            return label;

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
