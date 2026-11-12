// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace NCode.Identity.AspNetCore.ProxyClientCertificate;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the proxy client certificate requirement.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class ProxyClientCertificateLog
{
    [LoggerMessage(
        EventId = EventIds.ProxyClientCertificateRefused,
        Level = LogLevel.Warning,
        Message = "A request without a trusted proxy client certificate on its connection was refused: {Reason}."
    )]
    internal static partial void ProxyClientCertificateRefused(
        this ILogger logger,
        ProxyClientCertificateRefusal reason
    );

    [LoggerMessage(
        EventId = EventIds.ProxyClientCertificateMissing,
        Level = LogLevel.Warning,
        Message = "A request without a trusted proxy client certificate on its connection was refused: missing."
    )]
    internal static partial void ProxyClientCertificateMissing(this ILogger logger);
}
