using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Playground.Results
{
    // https://github.com/dotnet/aspnetcore/blob/master/src/Mvc/Mvc.Core/src/JsonResult.cs

    /// <summary>
    /// Represents an <see cref="IHttpResult"/> that when executed will
    /// produce an HTTP response with the given response status code
    /// and formats the given object as JSON.
    /// </summary>
    public class ObjectResult<T> : IHttpResult
    {
        /// <summary>
        /// Creates a new <see cref="ObjectResult{T}"/> with the given <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The value to format as JSON.</param>
        public ObjectResult(T value)
        {
            Value = value;
        }

        /// <summary>
        /// Creates a new <see cref="ObjectResult{T}"/> with the given <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The value to format as JSON.</param>
        /// <param name="serializerOptions">The serializer settings to be used by the formatter.</param>
        public ObjectResult(T value, JsonSerializerOptions serializerOptions)
        {
            Value = value;
            SerializerOptions = serializerOptions;
        }

        /// <summary>
        /// Gets or sets the value to be formatted.
        /// </summary>
        public T Value { get; set; }

        /// <summary>
        /// Gets or sets the serializer settings.
        /// </summary>
        public JsonSerializerOptions? SerializerOptions { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        public int? StatusCode { get; set; }

        /// <inheritdoc />
        public virtual async Task ExecuteAsync(HttpContext httpContext)
        {
            if (httpContext == null)
                throw new ArgumentNullException(nameof(httpContext));

            var statusCode = StatusCode;
            if (statusCode != null)
                httpContext.Response.StatusCode = statusCode.Value;

            var value = Value;
            var type = value?.GetType() ?? typeof(T);
            var options = SerializerOptions;
            var cancellationToken = httpContext.RequestAborted;

            // WriteAsJsonAsync already sets ContentType
            await httpContext.Response.WriteAsJsonAsync(value, type, options, cancellationToken);
        }
    }
}
