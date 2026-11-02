// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Json;

/// <summary>
/// Represents the serialized JSON shape of a <see cref="Uri"/> — its escaped value plus whether it is absolute.
/// </summary>
internal sealed class UriEnvelope
{
    public bool IsAbsolute { get; init; }
    public required string EscapedValue { get; init; }
}
