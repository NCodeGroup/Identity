// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// The serializable name and value of the anti-forgery request header that the console's bundle sends to the BFF.
/// </summary>
internal sealed record ManagementConsoleHeaderDocument
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("value")]
    public required string Value { get; init; }
}
