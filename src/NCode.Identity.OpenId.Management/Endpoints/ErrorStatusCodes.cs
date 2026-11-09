// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// The HTTP status codes that report an error: a client error or a server error.
/// </summary>
internal static class ErrorStatusCodes
{
    /// <summary>
    /// The last status code of the server error class.
    /// </summary>
    public const int LastServerError = 599;

    /// <summary>
    /// Determines whether a status code reports a client or a server error.
    /// </summary>
    /// <param name="statusCode">The status code to test.</param>
    /// <returns><see langword="true"/> when the status code is from <c>400</c> to <c>599</c>.</returns>
    public static bool Contains(int statusCode) =>
        statusCode is >= StatusCodes.Status400BadRequest and <= LastServerError;
}
