// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Converters;

/// <summary>
/// Provides an implementation of <see cref="ValueConverter"/> that converts <see cref="JsonElement"/> to a JSON string and back.
/// This is useful for storing JSON data in a database column as a string.
/// </summary>
internal class JsonElementConverter : ValueConverter<JsonElement, string>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonElementConverter"/> class.
    /// </summary>
    public JsonElementConverter()
        : base(
            value => value.GetRawText(),
            value => JsonElement.Parse(value),
            new ConverterMappingHints(unicode: true)
        )
    {
        // nothing
    }
}
