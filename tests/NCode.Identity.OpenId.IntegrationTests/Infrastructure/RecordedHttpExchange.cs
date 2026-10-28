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

using System.Net;

namespace NCode.Identity.OpenId.IntegrationTests.Infrastructure;

/// <summary>
/// One request a client sent through a <see cref="RecordingHttpMessageHandler"/> and the status code it received.
/// </summary>
/// <param name="Path">The absolute path of the request URI, such as <c>/oauth2/par</c>.</param>
/// <param name="AuthorizationScheme">The scheme of the request's <c>Authorization</c> header, or <see langword="null"/>.</param>
/// <param name="Body">The request's body as text, or <see langword="null"/> when it has none.</param>
/// <param name="StatusCode">The response's status code.</param>
internal sealed record RecordedHttpExchange(
    string Path,
    string? AuthorizationScheme,
    string? Body,
    HttpStatusCode StatusCode
);
