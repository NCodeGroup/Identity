// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff.Requests;

/// <summary>
/// Reads the BFF's form posts without turning a malformed body into a server error.
/// </summary>
internal static class BffForms
{
    /// <summary>
    /// Reads the form body, or returns <c>null</c> when the body is malformed or over the form limits.
    /// </summary>
    /// <param name="request">The request to read.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    // A malformed body, or one over the form limits, throws InvalidDataException: the request is bad, not the server.
    public static async ValueTask<IFormCollection?> TryReadFormAsync(
        HttpRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }
}
