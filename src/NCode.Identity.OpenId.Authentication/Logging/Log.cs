#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

namespace NCode.Identity.OpenId.Authentication.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.OpenId.Authentication</c> package.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    [LoggerMessage(
        EventId = EventIds.AuthorizationRequestNotHandled,
        Level = LogLevel.Error,
        Message = "The authorization request was not handled."
    )]
    internal static partial void AuthorizationRequestNotHandled(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.ClientRequestedAccountCreation,
        Level = LogLevel.Information,
        Message = "Client requested account creation."
    )]
    internal static partial void ClientRequestedAccountCreation(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.ClientRequestedReAuthentication,
        Level = LogLevel.Information,
        Message = "Client requested re-authentication."
    )]
    internal static partial void ClientRequestedReAuthentication(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.FailedToDecodeJwt,
        Level = LogLevel.Warning,
        Message = "Failed to decode JWT"
    )]
    internal static partial void FailedToDecodeJwt(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = EventIds.FailedToDeserializeJson,
        Level = LogLevel.Warning,
        Message = "Failed to deserialize JSON"
    )]
    internal static partial void FailedToDeserializeJson(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = EventIds.FailedToFetchRequestUri,
        Level = LogLevel.Warning,
        Message = "Failed to fetch the request URI"
    )]
    internal static partial void FailedToFetchRequestUri(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = EventIds.MissingStateParameter,
        Level = LogLevel.Information,
        Message = "Missing 'state' parameter."
    )]
    internal static partial void MissingStateParameter(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.InvalidStateParameter,
        Level = LogLevel.Information,
        Message = "Invalid 'state' parameter."
    )]
    internal static partial void InvalidStateParameter(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.ContinueProviderNotHandled,
        Level = LogLevel.Error,
        Message = "The continue provider did not handle the request."
    )]
    internal static partial void ContinueProviderNotHandled(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.PasswordGrantNotSupported,
        Level = LogLevel.Warning,
        Message = "The resource owner password credential grant type is not supported. "
            + "Please register an implementation of "
            + "`ICommandResponseHandler<AuthenticatePasswordGrantCommand, AuthenticateSubjectDisposition>` "
            + "that can handle the resource owner password credential grant type."
    )]
    internal static partial void PasswordGrantNotSupported(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.SubjectValidationFailed,
        Level = LogLevel.Warning,
        Message = "Subject validation failed: {Reason}"
    )]
    internal static partial void SubjectValidationFailed(this ILogger logger, string reason);
}
