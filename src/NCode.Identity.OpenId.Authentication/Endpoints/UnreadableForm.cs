// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Builds the error that an endpoint answers when the request's form body cannot be read: a malformed body, or one over
/// the form limits, is the client's error.
/// </summary>
internal static class UnreadableForm
{
    /// <summary>
    /// Creates the <c>invalid_request</c> error, with status <c>400</c>, for a form body that cannot be read.
    /// </summary>
    /// <param name="errorFactory">The <see cref="IOpenIdErrorFactory"/> that creates the error.</param>
    public static IOpenIdError CreateError(IOpenIdErrorFactory errorFactory) =>
        errorFactory
            .InvalidRequest("The request body is not a valid form.")
            .WithStatusCode(StatusCodes.Status400BadRequest);
}
