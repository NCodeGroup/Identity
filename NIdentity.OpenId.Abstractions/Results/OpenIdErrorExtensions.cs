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

using NIdentity.OpenId.Exceptions;

namespace NIdentity.OpenId.Results;

/// <summary>
/// Provides extension methods for <see cref="IOpenIdError"/>.
/// </summary>
public static class OpenIdErrorExtensions
{
    /// <summary>
    /// Wraps the <see cref="IOpenIdError"/> in an <see cref="IOpenIdResult"/>.
    /// </summary>
    /// <param name="error">The <see cref="IOpenIdError"/> to wrap.</param>
    /// <returns>The <see cref="IOpenIdResult"/> instance.</returns>
    public static IOpenIdResult AsResult(this IOpenIdError error)
    {
        return new OpenIdErrorResult(error);
    }

    /// <summary>
    /// Wraps the <see cref="IOpenIdError"/> in an <see cref="OpenIdException"/>.
    /// </summary>
    /// <param name="error">The <see cref="IOpenIdError"/> to wrap.</param>
    /// <returns>The <see cref="OpenIdException"/> instance.</returns>
    public static OpenIdException AsException(this IOpenIdError error)
    {
        return new OpenIdException(error, error.Exception);
    }

    /// <summary>
    /// Wraps the <see cref="IOpenIdError"/> in an <see cref="OpenIdException"/>.
    /// </summary>
    /// <param name="error">The <see cref="IOpenIdError"/> to wrap.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <returns>The <see cref="OpenIdException"/> instance.</returns>
    public static OpenIdException AsException(this IOpenIdError error, string? message)
    {
        return new OpenIdException(error, message, error.Exception);
    }

    /// <summary>
    /// Sets the HTTP status code to be used when returning a response.
    /// </summary>
    /// <param name="error">The <see cref="IOpenIdError"/> to update.</param>
    /// <param name="statusCode">The value to set.</param>
    /// <returns>The <see cref="IOpenIdError"/> instance.</returns>
    public static IOpenIdError WithStatusCode(this IOpenIdError error, int? statusCode)
    {
        error.StatusCode = statusCode;
        return error;
    }

    /// <summary>
    /// Sets the <see cref="Exception"/> that triggered the <c>OAuth</c> or <c>OpenID Connect</c> error.
    /// </summary>
    /// <param name="error">The <see cref="IOpenIdError"/> to update.</param>
    /// <param name="exception">The value to set.</param>
    /// <returns>The <see cref="IOpenIdError"/> instance.</returns>
    public static IOpenIdError WithException(this IOpenIdError error, Exception? exception)
    {
        error.Exception = exception;
        return error;
    }

    /// <summary>
    /// Sets the <c>error_description</c> parameter.
    /// </summary>
    /// <param name="error">The <see cref="IOpenIdError"/> to update.</param>
    /// <param name="description">The value to set.</param>
    /// <returns>The <see cref="IOpenIdError"/> instance.</returns>
    public static IOpenIdError WithDescription(this IOpenIdError error, string? description)
    {
        error.Description = description;
        return error;
    }

    /// <summary>
    /// Sets the <c>error_uri</c> parameter.
    /// </summary>
    /// <param name="error">The <see cref="IOpenIdError"/> to update.</param>
    /// <param name="uri">The value to set.</param>
    /// <returns>The <see cref="IOpenIdError"/> instance.</returns>
    public static IOpenIdError WithUri(this IOpenIdError error, Uri? uri)
    {
        error.Uri = uri;
        return error;
    }

    /// <summary>
    /// Sets the <c>state</c> parameter.
    /// </summary>
    /// <param name="error">The <see cref="IOpenIdError"/> to update.</param>
    /// <param name="state">The value to set.</param>
    /// <returns>The <see cref="IOpenIdError"/> instance.</returns>
    public static IOpenIdError WithState(this IOpenIdError error, string? state)
    {
        error.State = state;
        return error;
    }
}
