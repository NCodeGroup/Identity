// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff;

/// <summary>
/// Builds the JSON document that <c>GET /bff/user</c> answers.
/// </summary>
[PublicAPI]
public interface IBffUserDocumentFactory
{
    /// <summary>
    /// Builds the document of the session that a request carries: <c>sub</c>, <c>name</c>, the frontend's allow-listed
    /// <c>claims</c>, <c>session</c>, <c>logoutUrl</c>, <c>logoutSid</c> and the fields of the contributors.
    /// </summary>
    /// <param name="httpContext">The request to an endpoint that the BFF guards.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The document, or <see langword="null"/> when the request has no session.</returns>
    /// <exception cref="InvalidOperationException">The request is to an endpoint that the BFF does not guard.</exception>
    ValueTask<JsonElement?> CreateAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    );
}
