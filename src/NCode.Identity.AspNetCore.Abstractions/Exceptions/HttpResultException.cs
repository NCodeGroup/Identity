// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Exceptions;

/// <summary>
/// Represents an error that occurred while executing an <c>HTTP</c> endpoint.
/// </summary>
[PublicAPI]
public class HttpResultException : Exception
{
    /// <summary>
    /// Gets the HTTP <see cref="IResult"/> associated with the exception.
    /// </summary>
    public IResult HttpResult { get; }

    private static string? GetMessage(IResult httpResult) =>
        httpResult is IValueHttpResult<string> valueResult ? valueResult.Value : null;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpResultException"/> class.
    /// </summary>
    /// <param name="httpResult">The HTTP <see cref="IResult"/> to associate with the exception.</param>
    public HttpResultException(IResult httpResult)
        : this(httpResult, null, null)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpResultException"/> class.
    /// </summary>
    /// <param name="httpResult">The HTTP <see cref="IResult"/> to associate with the exception.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public HttpResultException(IResult httpResult, string? message)
        : this(httpResult, message, null)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpResultException"/> class.
    /// </summary>
    /// <param name="httpResult">The HTTP <see cref="IResult"/> to associate with the exception.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a <c>null</c>
    /// reference if no inner exception is specified.</param>
    public HttpResultException(IResult httpResult, string? message, Exception? innerException)
        : base(message ?? GetMessage(httpResult) ?? innerException?.Message, innerException)
    {
        HttpResult = httpResult;
    }
}
