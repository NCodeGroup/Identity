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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Results;

/// <summary>
/// Describes a negotiated write handed to an <see cref="IOpenIdResponseFormatter"/>: the request's
/// <see cref="HttpContext"/>, the <see cref="IOpenIdResponse"/> being written, the negotiated
/// <see cref="MediaType"/> the formatter should emit (one of its <see cref="IOpenIdResponseFormatter.MediaTypes"/>),
/// and the <see cref="StatusCode"/> resolved from the response. New inputs are added as init-only members so the
/// seam evolves without a breaking signature change.
/// </summary>
[PublicAPI]
public readonly record struct OpenIdResponseFormatterContext
{
    /// <summary>
    /// Gets the <see cref="HttpContext"/> for the current request.
    /// </summary>
    public required HttpContext HttpContext { get; init; }

    /// <summary>
    /// Gets the <see cref="IOpenIdResponse"/> being written.
    /// </summary>
    public required IOpenIdResponse Response { get; init; }

    /// <summary>
    /// Gets the negotiated media type the formatter should emit; one of the formatter's
    /// <see cref="IOpenIdResponseFormatter.MediaTypes"/>.
    /// </summary>
    public required string MediaType { get; init; }

    /// <summary>
    /// Gets the HTTP status code resolved from the response, or <see langword="null"/> when the response does not
    /// specify one.
    /// </summary>
    public int? StatusCode { get; init; }
}
