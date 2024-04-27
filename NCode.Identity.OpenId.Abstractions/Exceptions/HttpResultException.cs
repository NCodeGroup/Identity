#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Exceptions;

/// <summary>
/// Represents an error that occured while executing a <c>HTTP</c> endpoint.
/// </summary>
public class HttpResultException : Exception
{
    /// <summary>
    /// Gets the HTTP <see cref="IResult"/> associated with the exception.
    /// </summary>
    public IResult HttpResult { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpResultException"/> class.
    /// </summary>
    /// <param name="httpResult">The HTTP <see cref="IResult"/> to associate with the exception.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public HttpResultException(IResult httpResult, string? message = null)
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
    public HttpResultException(IResult httpResult, string? message, Exception? innerException = null)
        : base(message ?? innerException?.Message, innerException)
    {
        HttpResult = httpResult;
    }
}
