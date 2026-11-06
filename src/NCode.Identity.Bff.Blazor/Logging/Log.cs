// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace NCode.Identity.Bff.Blazor.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.Bff.Blazor</c> package.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    [LoggerMessage(
        EventId = EventIds.RevalidationFaulted,
        Level = LogLevel.Error,
        Message = "Waiting for the BFF session of a Blazor circuit to end failed; the circuit is signed out"
    )]
    internal static partial void RevalidationFaulted(this ILogger logger, Exception exception);
}
