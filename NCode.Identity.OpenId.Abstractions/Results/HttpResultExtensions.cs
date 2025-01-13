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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Results;

/// <summary>
/// Provides extension methods for <see cref="IResult"/>.
/// </summary>
[PublicAPI]
public static class HttpResultExtensions
{
    /// <summary>
    /// Wraps the <see cref="IOpenIdResponse"/> in an <see cref="IResult"/>.
    /// </summary>
    /// <param name="response">The <see cref="IOpenIdResponse"/> to wrap.</param>
    /// <returns>The <see cref="IResult"/> instance.</returns>
    public static OpenIdResult<T> AsResult<T>(this T response)
        where T : class, IOpenIdResponse
    {
        return new OpenIdResult<T>(response);
    }

    /// <summary>
    /// Wraps the HTTP <see cref="IResult"/> in a <see cref="HttpResultException"/>.
    /// </summary>
    /// <param name="httpResult">The HTTP <see cref="IResult"/> to wrap.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <returns>The <see cref="HttpResultException"/> instance.</returns>
    public static HttpResultException AsException(this IResult httpResult, string? message = null) =>
        new(httpResult, message);
}
