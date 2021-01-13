using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace NCode.Identity.Results
{
    // https://github.com/dotnet/aspnetcore/blob/master/src/Mvc/Mvc.Core/src/OkObjectResult.cs

    /// <summary>
    /// An <see cref="ObjectResult{T}"/> result which formats the given object as JSON and
    /// will produce a <see cref="StatusCodes.Status200OK"/> response.
    /// </summary>
    public class OkObjectResult<T> : ObjectResult<T>
    {
        private const int DefaultStatusCode = StatusCodes.Status200OK;

        /// <summary>
        /// Initializes a new instance of the <see cref="OkObjectResult{T}"/> class.
        /// </summary>
        /// <param name="value">The value to format as JSON.</param>
        public OkObjectResult(T value)
            : base(value)
        {
            StatusCode = DefaultStatusCode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OkObjectResult{T}"/> class.
        /// </summary>
        /// <param name="value">The value to format as JSON.</param>
        /// <param name="serializerOptions">The serializer settings to be used by the formatter.</param>
        public OkObjectResult(T value, JsonSerializerOptions serializerOptions)
            : base(value, serializerOptions)
        {
            StatusCode = DefaultStatusCode;
        }
    }
}
