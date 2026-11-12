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
    /// <summary>
    /// The reason when the connection carries no certificate.
    /// </summary>
    internal const string MissingReason = "missing";

    /// <summary>
    /// The reason when the connection's certificate is not the proxy's.
    /// </summary>
    internal const string UntrustedReason = "untrusted";

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "A request without a trusted proxy client certificate on its connection was refused: {Reason}."
    )]
    internal static partial void ProxyClientCertificateRefused(this ILogger logger, string reason);
}
