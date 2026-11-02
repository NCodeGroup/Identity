// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Exceptions;

namespace NCode.Identity.Results;

/// <summary>
/// Provides extension methods for <see cref="IResult"/>.
/// </summary>
[PublicAPI]
public static class HttpResultExtensions
{
    extension(IResult httpResult)
    {
        /// <summary>
        /// Wraps the HTTP <see cref="IResult"/> in a <see cref="HttpResultException"/>.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <returns>The <see cref="HttpResultException"/> instance.</returns>
        public HttpResultException AsException(string? message = null) => new(httpResult, message);
    }
}
