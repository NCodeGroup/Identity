// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff.Requests;

/// <summary>
/// Reads the BFF's form posts without turning a malformed body into a server error.
/// </summary>
// The OpenID packages read forms through their own FormRequestExtensions.TryReadFormAsync: neither package may reference
// the other (ADR-1016), and an Abstractions package holds no method body, so each keeps this one contract.
internal static class BffForms
{
    /// <summary>
    /// Reads the form body, or returns <c>null</c> when the request carries no form or its body is malformed or over the
    /// form limits.
    /// </summary>
    /// <param name="request">The request to read.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    // A malformed body, or one over the form limits, throws InvalidDataException: the request is bad, not the server.
    public static async ValueTask<IFormCollection?> TryReadFormAsync(
        HttpRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!request.HasFormContentType)
        {
            return null;
        }

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
