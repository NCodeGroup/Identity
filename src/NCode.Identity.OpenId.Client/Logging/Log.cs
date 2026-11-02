// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace NCode.Identity.OpenId.Client.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.OpenId.Client</c> package.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    [LoggerMessage(
        EventId = EventIds.ProviderMetadataFetchFailed,
        Level = LogLevel.Warning,
        Message = "The OpenID Provider metadata for '{Authority}' could not be retrieved: {Reason}"
    )]
    internal static partial void ProviderMetadataFetchFailed(
        this ILogger logger,
        string authority,
        string reason,
        Exception? exception
    );

    [LoggerMessage(
        EventId = EventIds.ProviderKeySetFetchFailed,
        Level = LogLevel.Warning,
        Message = "The OpenID Provider signing keys at '{JwksUri}' could not be retrieved: {Reason}"
    )]
    internal static partial void ProviderKeySetFetchFailed(
        this ILogger logger,
        string jwksUri,
        string reason,
        Exception? exception
    );
}
