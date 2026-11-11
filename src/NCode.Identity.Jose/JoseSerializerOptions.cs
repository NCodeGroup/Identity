// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Json;

namespace NCode.Identity.Jose;

/// <summary>
/// Contains options for Jose services and algorithms.
/// </summary>
[PublicAPI]
public sealed class JoseSerializerOptions
{
    /// <summary>
    /// Gets or sets a list containing the codes of all the disabled algorithms.
    /// </summary>
    public List<string> DisabledAlgorithms { get; set; } = [];

    /// <summary>
    /// Gets the <see cref="JsonSerializerOptions"/> that is used for JSON serialization.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; } =
        new(JsonSerializerDefaults.Web) { Converters = { ObjectJsonConverter.Default } };
}
