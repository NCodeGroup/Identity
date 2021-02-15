using System.Text.Json;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Playground.Results
{
    /// <summary>
    /// Provides <see cref="IHttpResult"/> factory functions that represent the result of a HTTP operation.
    /// </summary>
    public static class HttpResultFactory
    {
        /// <summary>
        /// Returns an instance of <see cref="OkResult"/>.
        /// </summary>
        public static IHttpResult Ok() => new OkResult();

        /// <summary>
        /// Returns an instance of <see cref="OkObjectResult{T}"/>.
        /// </summary>
        public static IHttpResult Ok<T>(T value) => new OkObjectResult<T>(value);

        /// <summary>
        /// Returns an instance of <see cref="StatusCodeResult"/>.
        /// </summary>
        public static IHttpResult StatusCode(int statusCode) => new StatusCodeResult(statusCode);

        /// <summary>
        /// Returns an instance of <see cref="ObjectResult{T}"/>.
        /// </summary>
        public static IHttpResult Object<T>(T value) => new ObjectResult<T>(value);

        /// <summary>
        /// Returns an instance of <see cref="ObjectResult{T}"/>.
        /// </summary>
        public static IHttpResult Object<T>(T value, JsonSerializerOptions serializerOptions) => new ObjectResult<T>(value, serializerOptions);

        /// <summary>
        /// Returns an instance of <see cref="ObjectResult{T}"/>.
        /// </summary>
        public static IHttpResult Object<T>(int statusCode, T value) => new ObjectResult<T>(value) { StatusCode = statusCode };

        /// <summary>
        /// Returns an instance of <see cref="ObjectResult{T}"/>
        /// </summary>
        public static IHttpResult Object<T>(int statusCode, T value, JsonSerializerOptions serializerOptions) => new ObjectResult<T>(value, serializerOptions) { StatusCode = statusCode };

        /// <summary>
        /// Returns an instance of <see cref="NotFoundResult"/>.
        /// </summary>
        public static IHttpResult NotFound() => new NotFoundResult();

        /// <summary>
        /// Returns an instance of <see cref="UnauthorizedResult"/>.
        /// </summary>
        public static IHttpResult Unauthorized(string challenge, string realm) => new UnauthorizedResult(challenge, realm);

        /// <summary>
        /// Returns an instance of <see cref="UnauthorizedResult"/>.
        /// </summary>
        public static IHttpResult Unauthorized(string challenge, string realm, IErrorDetails errorDetails) => new UnauthorizedResult(challenge, realm) { ErrorDetails = errorDetails };

        /// <summary>
        /// Returns an instance of <see cref="BadRequestResult"/>.
        /// </summary>
        public static IHttpResult BadRequest(string error) => new BadRequestResult(error);

        /// <summary>
        /// Returns an instance of <see cref="BadRequestResult"/>.
        /// </summary>
        public static IHttpResult BadRequest(string error, string errorDescription) => new BadRequestResult(error, errorDescription);

        /// <summary>
        /// Returns an instance of <see cref="BadRequestResult"/>.
        /// </summary>
        public static IHttpResult BadRequest(IErrorDetails errorDetails) => new BadRequestResult(errorDetails);
    }
}
