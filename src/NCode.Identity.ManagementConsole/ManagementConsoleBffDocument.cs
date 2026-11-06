// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// The serializable coordinates of the BFF that the console's bundle calls: the path its endpoints are mapped at and
/// the anti-forgery header its requests carry.
/// </summary>
internal sealed record ManagementConsoleBffDocument
{
    [JsonPropertyName("basePath")]
    public required string BasePath { get; init; }

    [JsonPropertyName("antiforgeryHeader")]
    public required ManagementConsoleHeaderDocument AntiforgeryHeader { get; init; }
}
