#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;

namespace NCode.Identity.Results;

/// <summary>
/// Provides the ability to associate additional HTTP response headers with the current instance.
/// Used to emit protocol headers such as <c>WWW-Authenticate</c> challenges (RFC 7235) alongside a rendered response.
/// </summary>
[PublicAPI]
public interface ISupportResponseHeaders
{
    /// <summary>
    /// Gets the collection of HTTP response headers to write when rendering the response, keyed by header name,
    /// or <see langword="null"/> when no additional headers have been added.
    /// </summary>
    IReadOnlyDictionary<string, StringValues>? ResponseHeaders { get; }

    /// <summary>
    /// Adds or replaces an HTTP response header to be written when rendering the response.
    /// </summary>
    /// <param name="name">The name of the HTTP response header.</param>
    /// <param name="value">The value of the HTTP response header.</param>
    void SetResponseHeader(string name, StringValues value);
}
