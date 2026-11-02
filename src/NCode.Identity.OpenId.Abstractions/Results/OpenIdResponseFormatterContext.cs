// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
