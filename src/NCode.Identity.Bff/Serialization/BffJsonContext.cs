// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;
using NCode.Identity.Bff.Login;
using NCode.Identity.Bff.Responses;
using NCode.Identity.Bff.Sessions;

namespace NCode.Identity.Bff.Serialization;

/// <summary>
/// The source-generated JSON context for the BFF session metadata and payload.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(BffSessionMetadata))]
[JsonSerializable(typeof(BffSessionPayload))]
[JsonSerializable(typeof(BffProblem))]
[JsonSerializable(typeof(BffLoginTransaction))]
internal sealed partial class BffJsonContext : JsonSerializerContext;
