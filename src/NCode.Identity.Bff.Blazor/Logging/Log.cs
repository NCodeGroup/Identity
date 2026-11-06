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
        EventId = EventIds.RevalidationFailed,
        Level = LogLevel.Warning,
        Message = "The BFF could not revalidate the session of a Blazor circuit"
    )]
    internal static partial void RevalidationFailed(this ILogger logger, Exception exception);
}
