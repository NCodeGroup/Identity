using System.Text.Json;
using NCode.Identity.Validation;

namespace NCode.Identity.Results
{
    public static class HttpResultFactory
    {
        public static IHttpResult Ok() => new OkResult();
        public static IHttpResult Ok<T>(T value) => new OkObjectResult<T>(value);

        public static IHttpResult StatusCode(int statusCode) => new StatusCodeResult(statusCode);

        public static IHttpResult Object<T>(T value) => new ObjectResult<T>(value);
        public static IHttpResult Object<T>(T value, JsonSerializerOptions serializerOptions) => new ObjectResult<T>(value, serializerOptions);

        public static IHttpResult Object<T>(int statusCode, T value) => new ObjectResult<T>(value) { StatusCode = statusCode };
        public static IHttpResult Object<T>(int statusCode, T value, JsonSerializerOptions serializerOptions) => new ObjectResult<T>(value, serializerOptions) { StatusCode = statusCode };

        public static IHttpResult NotFound() => new NotFoundResult();

        public static IHttpResult Unauthorized(string challenge, string realm) => new UnauthorizedResult(challenge, realm);
        public static IHttpResult Unauthorized(string challenge, string realm, IErrorDetails errorDetails) => new UnauthorizedResult(challenge, realm) { ErrorDetails = errorDetails };

        public static IHttpResult BadRequest(string error) => new BadRequestResult(error);
        public static IHttpResult BadRequest(string error, string errorDescription) => new BadRequestResult(error, errorDescription);
        public static IHttpResult BadRequest(IErrorDetails errorDetails) => new BadRequestResult(errorDetails);
    }
}
