// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace NCode.Identity.Bff.Blazor.Client.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.Bff.Blazor.Client</c> package.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    [LoggerMessage(
        EventId = EventIds.ProbeFailed,
        Level = LogLevel.Warning,
        Message = "The BFF session probe failed: {Reason}; the current state is kept"
    )]
    internal static partial void ProbeFailed(
        this ILogger logger,
        string reason,
        Exception? exception
    );
}
