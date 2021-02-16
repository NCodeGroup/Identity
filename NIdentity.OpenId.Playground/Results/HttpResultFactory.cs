using System.Text.Json;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Playground.Results
{
    internal class HttpResultFactory : IHttpResultFactory
    {
        public IHttpResult Ok() => new OkResult();

        public IHttpResult Ok<T>(T value) => new OkObjectResult<T>(value);

        public IHttpResult StatusCode(int statusCode) => new StatusCodeResult(statusCode);

        public IHttpResult Object<T>(int statusCode, T value) => new ObjectResult<T>(value) { StatusCode = statusCode };

        public IHttpResult Object<T>(int statusCode, T value, JsonSerializerOptions serializerOptions) => new ObjectResult<T>(value, serializerOptions) { StatusCode = statusCode };

        public IHttpResult NotFound() => new NotFoundResult();

        public IHttpResult Unauthorized(string challenge, string realm) => new UnauthorizedResult(challenge, realm);

        public IHttpResult Unauthorized(string challenge, string realm, IErrorDetails errorDetails) => new UnauthorizedResult(challenge, realm) { ErrorDetails = errorDetails };

        public IHttpResult BadRequest() => new BadRequestResult();

        public IHttpResult BadRequest<T>(T value) => new BadRequestObjectResult<T>(value);
    }
}
