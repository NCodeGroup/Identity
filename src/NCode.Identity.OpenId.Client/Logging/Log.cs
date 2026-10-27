#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
