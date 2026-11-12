// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.HttpLogging;

namespace NCode.Identity.AspNetCore;

// See ADR-1503.
/// <summary>
/// Provides the endpoint metadata that opts an endpoint in to HTTP logging of its request body, its response body, or
/// both.
/// </summary>
/// <param name="fields">The body fields that the endpoint opts in to.</param>
internal sealed class HttpBodyLoggingMetadata(HttpLoggingFields fields)
{
    /// <summary>
    /// Gets the opt-in for the request body.
    /// </summary>
    public static HttpBodyLoggingMetadata RequestBody { get; } = new(HttpLoggingFields.RequestBody);

    /// <summary>
    /// Gets the opt-in for the response body.
    /// </summary>
    public static HttpBodyLoggingMetadata ResponseBody { get; } =
        new(HttpLoggingFields.ResponseBody);

    /// <summary>
    /// Gets the body fields that the endpoint opts in to.
    /// </summary>
    public HttpLoggingFields Fields { get; } = fields;
}
