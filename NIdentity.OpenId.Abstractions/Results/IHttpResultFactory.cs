#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using System.Text.Json;

namespace NIdentity.OpenId.Results
{
    /// <summary>
    /// Provides factory functions to create instances of <see cref="IHttpResult"/> for various HTTP responses.
    /// </summary>
    public interface IHttpResultFactory
    {
        /// <summary>
        /// Creates an <see cref="IHttpResult"/> that produces a 200 OK response with an empty response payload.
        /// </summary>
        /// <returns>The created <see cref="IHttpResult"/> for the response.</returns>
        IHttpResult Ok();

        /// <summary>
        /// Creates an <see cref="IHttpResult"/> that produces a 200 OK response with the specified response payload.
        /// </summary>
        /// <param name="value">The value to return in the response payload.</param>
        /// <typeparam name="T">The type of the value to return in the response payload.</typeparam>
        /// <returns>The created <see cref="IHttpResult"/> for the response.</returns>
        IHttpResult Ok<T>(T value);

        /// <summary>
        /// Creates an <see cref="IHttpResult"/> that produces a response with the specified HTTP status code and an
        /// empty response payload.
        /// </summary>
        /// <param name="statusCode">The HTTP status code to set on the response.</param>
        /// <returns>The created <see cref="IHttpResult"/> for the response.</returns>
        IHttpResult StatusCode(int statusCode);

        /// <summary>
        /// Creates an <see cref="IHttpResult"/> that produces a response with the specified HTTP status code and the
        /// specified response payload.
        /// </summary>
        /// <param name="statusCode">The HTTP status code to set on the response.</param>
        /// <param name="value">The value to return in the response payload.</param>
        /// <typeparam name="T">The type of the value to return in the response payload.</typeparam>
        /// <returns>The created <see cref="IHttpResult"/> for the response.</returns>
        IHttpResult Object<T>(int statusCode, T value);

        /// <summary>
        /// Creates an <see cref="IHttpResult"/> that produces a response with the specified HTTP status code and the
        /// specified response payload.
        /// </summary>
        /// <param name="statusCode">The HTTP status code to set on the response.</param>
        /// <param name="value">The value to return in the response payload.</param>
        /// <param name="serializerOptions">The <see cref="JsonSerializerOptions"/> to use when serializing
        /// <paramref name="value"/> to JSON.</param>
        /// <typeparam name="T">The type of the value to return in the response payload.</typeparam>
        /// <returns>The created <see cref="IHttpResult"/> for the response.</returns>
        IHttpResult Object<T>(int statusCode, T value, JsonSerializerOptions serializerOptions);

        /// <summary>
        /// Creates an <see cref="IHttpResult"/> that produces a 400 Bad Request response with an empty response payload.
        /// </summary>
        /// <returns>The created <see cref="IHttpResult"/> for the response.</returns>
        IHttpResult BadRequest();

        /// <summary>
        /// Creates an <see cref="IHttpResult"/> that produces a 400 Bad Request response with the specified response payload.
        /// </summary>
        /// <param name="value">The value to return in the response payload.</param>
        /// <typeparam name="T">The type of the value to return in the response payload.</typeparam>
        /// <returns>The created <see cref="IHttpResult"/> for the response.</returns>
        IHttpResult BadRequest<T>(T value);

        /// <summary>
        /// Creates an <see cref="IHttpResult"/> that produces a 404 Not Found response with an empty response payload.
        /// </summary>
        /// <returns>The created <see cref="IHttpResult"/> for the response.</returns>
        IHttpResult NotFound();

        /// <summary>
        /// Creates an <see cref="IHttpResult"/> that produces a 404 Not Found response with the specified response payload.
        /// </summary>
        /// <param name="value">The value to return in the response payload.</param>
        /// <typeparam name="T">The type of the value to return in the response payload.</typeparam>
        /// <returns>The created <see cref="IHttpResult"/> for the response.</returns>
        IHttpResult NotFound<T>(T value);
    }
}
