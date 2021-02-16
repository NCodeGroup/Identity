using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NIdentity.OpenId.Validation
{
    /// <summary>
    /// Represents the response payload when an error occurs.
    /// </summary>
    public interface IErrorDetails
    {
        /// <summary>
        /// Gets or sets the HTTP status code to be used when returning a response.
        /// </summary>
        int? StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the <c>error</c> parameter.
        /// </summary>
        string Code { get; set; }

        /// <summary>
        /// Gets or sets the <c>error_description</c> parameter.
        /// </summary>
        string? Description { get; set; }

        /// <summary>
        /// Gets or sets the <c>error_uri</c> parameter.
        /// </summary>
        string? Uri { get; set; }

        /// <summary>
        /// Gets or sets a dictionary that is used to handle JSON serialization (or
        /// deserialization) of additional properties that aren't declared.
        /// </summary>
        Dictionary<string, object?> ExtensionData { get; }
    }

    /// <summary>
    /// Provides a default implementation for the <see cref="IErrorDetails"/> interface.
    /// </summary>
    public class ErrorDetails : IErrorDetails
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorDetails"/> class.
        /// </summary>
        /// <param name="code">The value for the <c>error</c> parameter.</param>
        public ErrorDetails(string code)
        {
            Code = code;
        }

        /// <inheritdoc />
        [JsonIgnore]
        public int? StatusCode { get; set; }

        /// <inheritdoc />
        [JsonPropertyName("error")]
        public string Code { get; set; }

        /// <inheritdoc />
        [JsonPropertyName("error_description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; set; }

        /// <inheritdoc />
        [JsonPropertyName("error_uri")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Uri { get; set; }

        /// <inheritdoc />
        [JsonExtensionData]
        public Dictionary<string, object?> ExtensionData { get; } = new();
    }
}
